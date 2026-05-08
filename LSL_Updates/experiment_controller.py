"""
experiment_controller.py

LSL-host-side controller that mirrors the Unity experiment buttons (Step,
Force Step, Session 1/2/3) and tracks the sequencer's state pushed back from
Unity via STATE messages.

Wire format mirror (must match Unity LslExperimentRouter.cs):
    out: "CMD:STEP:<seq>"
         "CMD:FORCE_STEP:<seq>"
         "CMD:SESSION:<int>:<seq>"
         "STATE_REQ"
    in:  "CMD_ACK:<seq>"
         "CMD_DONE:<seq>:ok"
         "CMD_DONE:<seq>:blocked"
         "CMD_DONE:<seq>:error:<msg>"
         "CMD_REJECT:<seq>:<reason>"
         "STATE:session=<n>,session_label=<lbl>,trial=<k>,total=<t>,violation=<v>,gsm=<g>"
         "READY:no_subject"
         "READY:subject=<id>"

Reliability:
    - Outbound CMDs use a sequence number and a retry timer. CMD_ACK arrives
      fast (Unity acknowledges on dispatch); CMD_DONE arrives after the
      action runs. We retry only until CMD_ACK; CMD_DONE late-arrival is fine.
      Unity dedupes by seq, so our retransmits are safe.
    - STATE pushes are unsolicited from Unity. We replace the cache on each
      one. STATE_REQ asks for an immediate re-push (used after reconnect).
"""

from __future__ import annotations

import enum
import logging
import threading
import time
from dataclasses import dataclass, field
from typing import Callable, Optional

log = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# State snapshot
# ---------------------------------------------------------------------------

@dataclass
class SequencerState:
    """Last known state of Unity's ExperimentSequencer."""
    session_index: int = -1            # -1 = unknown / not yet armed
    session_label: str = "—"
    trial: int = 0                     # 1-based when armed; 0 before
    total_trials: int = 0
    violation: str = "—"               # last stimulus label or "Normal"
    gsm_state: str = "—"
    last_update_monotonic: float = 0.0

    def is_armed(self) -> bool:
        return self.session_index >= 0


# ---------------------------------------------------------------------------
# Outbound command bookkeeping
# ---------------------------------------------------------------------------

class CmdResult(enum.Enum):
    PENDING = "pending"   # in-flight, awaiting CMD_ACK
    ACKED = "acked"       # ACK received; awaiting CMD_DONE
    OK = "ok"
    BLOCKED = "blocked"
    REJECTED = "rejected"
    ERROR = "error"
    TIMEOUT = "timeout"


@dataclass
class _PendingCmd:
    seq: str
    wire: str
    attempts: int = 0
    timer: Optional[threading.Timer] = None
    result: CmdResult = CmdResult.PENDING
    detail: str = ""
    on_done: Optional[Callable[["_PendingCmd"], None]] = None


# ---------------------------------------------------------------------------
# Controller
# ---------------------------------------------------------------------------

class ExperimentController:
    """
    Manages outbound CMD:* dispatch and inbound STATE/CMD_*/READY parsing.

    Typical use::

        ctrl = ExperimentController(
            send=send_udp,
            on_state=lambda s: ui.update_state_panel(s),
            on_command_done=lambda r: ui.flash_done(r),
            on_ready=lambda info: ui.show_handshake_ok(info),
        )

        ui.btn_step.clicked.connect(ctrl.send_step)
        ui.btn_force_step.clicked.connect(ctrl.send_force_step)
        ui.btn_session_1.clicked.connect(lambda: ctrl.send_session(1))

        # in your UDP receive callback:
        if not ctrl.handle_inbound(msg):
            # not consumed, fall through to other handlers
            ...
    """

    DEFAULT_ACK_TIMEOUT_SEC = 0.25
    DEFAULT_MAX_RETRIES = 4   # initial + 3 retries

    # READY parser is shared between handshake reconnect detection and the
    # state panel.
    READY_PREFIX = "READY:"

    def __init__(
        self,
        send: Callable[[str], None],
        *,
        on_state: Optional[Callable[[SequencerState], None]] = None,
        on_command_done: Optional[Callable[[_PendingCmd], None]] = None,
        on_ready: Optional[Callable[[dict], None]] = None,
        ack_timeout_sec: float = DEFAULT_ACK_TIMEOUT_SEC,
        max_retries: int = DEFAULT_MAX_RETRIES,
    ) -> None:
        self._send = send
        self._on_state = on_state
        self._on_command_done = on_command_done
        self._on_ready = on_ready
        self._ack_timeout = float(ack_timeout_sec)
        self._max_retries = int(max_retries)

        self._lock = threading.RLock()
        self._seq_counter = 0
        self._pending: dict[str, _PendingCmd] = {}
        self._state = SequencerState()

    # ── public state inspection ──────────────────────────────────────────

    @property
    def state(self) -> SequencerState:
        with self._lock:
            # Return a shallow copy to keep callers from mutating internal state.
            s = self._state
            return SequencerState(
                session_index=s.session_index,
                session_label=s.session_label,
                trial=s.trial,
                total_trials=s.total_trials,
                violation=s.violation,
                gsm_state=s.gsm_state,
                last_update_monotonic=s.last_update_monotonic,
            )

    def has_outstanding_command(self) -> bool:
        with self._lock:
            return bool(self._pending)

    # ── outbound: experiment buttons ─────────────────────────────────────

    def send_step(self,
                  on_done: Optional[Callable[[_PendingCmd], None]] = None) -> str:
        return self._dispatch("STEP", on_done=on_done)

    def send_force_step(self,
                        on_done: Optional[Callable[[_PendingCmd], None]] = None) -> str:
        return self._dispatch("FORCE_STEP", on_done=on_done)

    def send_session(self, session_number: int,
                     on_done: Optional[Callable[[_PendingCmd], None]] = None) -> str:
        if not isinstance(session_number, int) or session_number < 1:
            raise ValueError(f"session_number must be int >= 1, got {session_number!r}")
        return self._dispatch(f"SESSION:{session_number}", on_done=on_done)

    def request_state(self) -> None:
        """Ask Unity to re-push its STATE. Use after reconnect or UI refresh."""
        try:
            self._send("STATE_REQ")
        except Exception:
            log.exception("STATE_REQ send failed")

    # ── inbound ──────────────────────────────────────────────────────────

    def handle_inbound(self, msg: str) -> bool:
        """
        Returns True if the message was consumed (CMD_ACK / CMD_DONE / CMD_REJECT
        / STATE / READY), False otherwise.
        """
        if not msg:
            return False
        if msg.startswith("CMD_ACK:"):    return self._handle_cmd_ack(msg)
        if msg.startswith("CMD_DONE:"):   return self._handle_cmd_done(msg)
        if msg.startswith("CMD_REJECT:"): return self._handle_cmd_reject(msg)
        if msg.startswith("STATE:"):      return self._handle_state(msg)
        if msg.startswith(self.READY_PREFIX): return self._handle_ready(msg)
        return False

    # ── internals: outbound ──────────────────────────────────────────────

    def _next_seq(self) -> str:
        self._seq_counter += 1
        # Prefix to keep CMD seqs distinct from SUBJECT_ID seqs in logs.
        return f"c{self._seq_counter}"

    def _dispatch(self, verb: str,
                  on_done: Optional[Callable[[_PendingCmd], None]]) -> str:
        with self._lock:
            seq = self._next_seq()
            wire = f"CMD:{verb}:{seq}"
            cmd = _PendingCmd(seq=seq, wire=wire, on_done=on_done)
            self._pending[seq] = cmd
            self._send_one(cmd)
        return seq

    def _send_one(self, cmd: _PendingCmd) -> None:
        # Caller holds _lock.
        try:
            self._send(cmd.wire)
        except Exception:
            log.exception("send failed for %r — will retry", cmd.wire)
        cmd.attempts += 1
        cmd.timer = threading.Timer(self._ack_timeout, self._on_ack_timeout, args=(cmd.seq,))
        cmd.timer.daemon = True
        cmd.timer.start()

    def _on_ack_timeout(self, seq: str) -> None:
        fire_for: Optional[_PendingCmd] = None
        with self._lock:
            cmd = self._pending.get(seq)
            if cmd is None or cmd.result != CmdResult.PENDING:
                return  # ACKed or completed in the meantime
            if cmd.attempts >= self._max_retries:
                cmd.result = CmdResult.TIMEOUT
                cmd.detail = "no_ack_timeout"
                self._cancel_timer(cmd)
                self._pending.pop(seq, None)
                fire_for = cmd
            else:
                self._send_one(cmd)

        if fire_for is not None:
            self._fire_done_outside_lock(fire_for)

    # ── internals: inbound ───────────────────────────────────────────────

    def _handle_cmd_ack(self, msg: str) -> bool:
        # CMD_ACK:<seq>
        parts = msg.split(":")
        if len(parts) != 2:
            log.warning("malformed CMD_ACK: %r", msg)
            return False
        seq = parts[1]
        with self._lock:
            cmd = self._pending.get(seq)
            if cmd is None:
                return True  # late ACK for a completed/timed-out cmd; absorb
            if cmd.result == CmdResult.PENDING:
                cmd.result = CmdResult.ACKED
                self._cancel_timer(cmd)  # stop retransmitting; CMD_DONE may still come
        return True

    def _handle_cmd_done(self, msg: str) -> bool:
        # CMD_DONE:<seq>:<result>[:<detail>...]
        parts = msg.split(":", 3)
        if len(parts) < 3:
            log.warning("malformed CMD_DONE: %r", msg)
            return False
        seq = parts[1]
        outcome = parts[2]
        detail = parts[3] if len(parts) >= 4 else ""

        fire_for: Optional[_PendingCmd] = None
        with self._lock:
            cmd = self._pending.pop(seq, None)
            if cmd is None:
                return True  # already removed (timeout/rejected)
            self._cancel_timer(cmd)
            if outcome == "ok":         cmd.result = CmdResult.OK
            elif outcome == "blocked":  cmd.result = CmdResult.BLOCKED
            elif outcome == "error":    cmd.result = CmdResult.ERROR
            else:                        cmd.result = CmdResult.ERROR
            cmd.detail = detail
            fire_for = cmd

        self._fire_done_outside_lock(fire_for)
        return True

    def _handle_cmd_reject(self, msg: str) -> bool:
        # CMD_REJECT:<seq>:<reason>
        parts = msg.split(":", 2)
        if len(parts) < 3:
            log.warning("malformed CMD_REJECT: %r", msg)
            return False
        seq = parts[1]
        reason = parts[2]

        fire_for: Optional[_PendingCmd] = None
        with self._lock:
            cmd = self._pending.pop(seq, None)
            if cmd is None:
                return True
            self._cancel_timer(cmd)
            cmd.result = CmdResult.REJECTED
            cmd.detail = reason
            fire_for = cmd

        self._fire_done_outside_lock(fire_for)
        return True

    def _handle_state(self, msg: str) -> bool:
        # STATE:k1=v1,k2=v2,...
        body = msg[len("STATE:"):]
        kv = {}
        for token in body.split(","):
            if "=" not in token:
                continue
            k, _, v = token.partition("=")
            kv[k.strip()] = v.strip()

        def _int(name: str, default: int) -> int:
            try: return int(kv.get(name, default))
            except (TypeError, ValueError): return default

        with self._lock:
            self._state = SequencerState(
                session_index=_int("session", -1),
                session_label=kv.get("session_label", "—"),
                trial=_int("trial", 0),
                total_trials=_int("total", 0),
                violation=kv.get("violation", "—"),
                gsm_state=kv.get("gsm", "—"),
                last_update_monotonic=time.monotonic(),
            )
            snapshot = self.state  # already a copy

        if self._on_state is not None:
            try:
                self._on_state(snapshot)
            except Exception:
                log.exception("on_state callback raised")
        return True

    def _handle_ready(self, msg: str) -> bool:
        # READY:no_subject  |  READY:subject=<id>
        body = msg[len(self.READY_PREFIX):]
        info = {"raw": body}
        if body == "no_subject":
            info["subject_id"] = None
        elif body.startswith("subject="):
            try:
                info["subject_id"] = int(body[len("subject="):])
            except ValueError:
                info["subject_id"] = None
        else:
            info["subject_id"] = None

        if self._on_ready is not None:
            try:
                self._on_ready(info)
            except Exception:
                log.exception("on_ready callback raised")
        return True

    # ── helpers ──────────────────────────────────────────────────────────

    def _cancel_timer(self, cmd: _PendingCmd) -> None:
        # Caller holds _lock.
        t = cmd.timer
        cmd.timer = None
        if t is not None:
            try: t.cancel()
            except Exception: pass

    def _fire_done_outside_lock(self, cmd: _PendingCmd) -> None:
        """Fires both the per-command on_done (if any) and the global
        on_command_done. The global one is the one main_window subscribes
        to for UI flashes; the per-command one is for callers that want a
        future-style result on a specific dispatch."""
        if cmd is None:
            return
        if cmd.on_done is not None:
            try:
                cmd.on_done(cmd)
            except Exception:
                log.exception("per-command on_done callback raised")
        if self._on_command_done is not None:
            try:
                self._on_command_done(cmd)
            except Exception:
                log.exception("on_command_done callback raised")


# ---------------------------------------------------------------------------
# Smoke test
# ---------------------------------------------------------------------------

if __name__ == "__main__":  # pragma: no cover
    logging.basicConfig(level=logging.DEBUG, format="%(levelname)s %(name)s | %(message)s")

    sent: list[str] = []
    states: list[SequencerState] = []
    dones: list[_PendingCmd] = []

    def fake_send(m: str) -> None:
        print("→", m)
        sent.append(m)

    def on_state(s: SequencerState) -> None:
        print("⇢ state:", s)
        states.append(s)

    def on_done(c: _PendingCmd) -> None:
        print("✓ done:", c.seq, c.result, c.detail)
        dones.append(c)

    ctrl = ExperimentController(send=fake_send, on_state=on_state, on_command_done=on_done,
                                ack_timeout_sec=0.05, max_retries=2)

    # Happy path STEP: ACK then DONE
    seq = ctrl.send_step()
    assert sent[-1] == f"CMD:STEP:{seq}"
    ctrl.handle_inbound(f"CMD_ACK:{seq}")
    ctrl.handle_inbound(f"CMD_DONE:{seq}:ok")
    assert dones[-1].result == CmdResult.OK

    # SESSION
    seq2 = ctrl.send_session(2)
    assert sent[-1] == f"CMD:SESSION:2:{seq2}"
    ctrl.handle_inbound(f"CMD_DONE:{seq2}:ok")  # immediate done is fine

    # STATE parse
    ctrl.handle_inbound("STATE:session=1,session_label=Tutorial,trial=3,total=10,violation=Normal,gsm=trial_start")
    assert ctrl.state.trial == 3
    assert ctrl.state.session_label == "Tutorial"

    # READY
    info_seen = []
    ctrl2 = ExperimentController(send=fake_send, on_ready=info_seen.append)
    ctrl2.handle_inbound("READY:subject=42")
    assert info_seen[-1]["subject_id"] == 42
    ctrl2.handle_inbound("READY:no_subject")
    assert info_seen[-1]["subject_id"] is None

    # ACK timeout → retransmit
    ctrl3 = ExperimentController(send=fake_send, ack_timeout_sec=0.02, max_retries=3)
    seq3 = ctrl3.send_step()
    time.sleep(0.1)
    # seq3 should have been retried; dedup on Unity side is what makes that safe
    assert sum(1 for m in sent if m.endswith(seq3)) >= 2

    print("\nALL SMOKE TESTS PASSED")
