"""
subject_id_handshake.py

State machine + retry loop for sending SUBJECT_ID:<n>:<seq> to the Unity host
and handling its SUBJECT_ID_ACK / SUBJECT_ID_REJECT replies.

Dependency-free by design — pass in a `send` callable (one that takes a string
and puts it on the wire) and a `clock` callable (default time.monotonic) for
testability. Subscribe to the `on_state_change` / `on_acked` / `on_rejected`
hooks to drive your UI.

Threading:
    All public methods are safe to call from the same thread that owns the
    instance (typically the LSL host's UDP receive thread + the Qt main
    thread coordinated via signals). The retry timer runs on a daemon
    threading.Timer; it doesn't lock anything that can block the UI.

Wire format mirror (must match Unity LslExperimentRouter.cs):
    out:  "SUBJECT_ID:<int>:<seq>"
          "SUBJECT_ID_OVERRIDE:<int>:<seq>"
    in:   "SUBJECT_ID_ACK:<id>:<seq>"
          "SUBJECT_ID_REJECT:<reason>:<seq>"
          "SUBJECT_ID_REJECT:already_set:<existing_id>:<seq>"   (4 colon-fields)
"""

from __future__ import annotations

import enum
import logging
import threading
import time
from dataclasses import dataclass
from typing import Callable, Optional

log = logging.getLogger(__name__)


class HandshakeState(enum.Enum):
    IDLE = "idle"          # nothing sent
    SENDING = "sending"    # in-flight, awaiting ACK
    ACKED = "acked"        # confirmed by Unity
    REJECTED = "rejected"  # Unity refused
    TIMEOUT = "timeout"    # max retries exhausted


@dataclass
class HandshakeResult:
    state: HandshakeState
    subject_id: Optional[int] = None
    reason: Optional[str] = None
    existing_id: Optional[int] = None  # set when reason == "already_set"


# ---------------------------------------------------------------------------
# Validation — kept here so callers don't reimplement it inconsistently.
# ---------------------------------------------------------------------------

SUBJECT_ID_MIN = 1
SUBJECT_ID_MAX = 9999


def is_valid_subject_id(value) -> bool:
    """True iff value is an int (or int-coercible string) in the allowed range."""
    try:
        n = int(value)
    except (TypeError, ValueError):
        return False
    return SUBJECT_ID_MIN <= n <= SUBJECT_ID_MAX


# ---------------------------------------------------------------------------
# Handshake
# ---------------------------------------------------------------------------

class SubjectIdHandshake:
    """
    State machine for the SUBJECT_ID exchange.

    Typical use::

        h = SubjectIdHandshake(send=send_udp,
                               on_acked=lambda id_: ui.lock_subject(id_),
                               on_rejected=lambda r: ui.show_error(r),
                               on_state_change=ui.update_status)

        if h.send_subject_id(42):           # call from UI button
            ...                              # waiting for ACK
        else:
            ui.show_error("invalid id")

        # in your UDP receive callback:
        h.handle_inbound(msg)
    """

    DEFAULT_RETRY_INTERVAL_SEC = 0.25
    DEFAULT_MAX_RETRIES = 4   # initial + 3 retries

    def __init__(
        self,
        send: Callable[[str], None],
        *,
        on_acked: Optional[Callable[[int], None]] = None,
        on_rejected: Optional[Callable[[HandshakeResult], None]] = None,
        on_state_change: Optional[Callable[[HandshakeState], None]] = None,
        retry_interval_sec: float = DEFAULT_RETRY_INTERVAL_SEC,
        max_retries: int = DEFAULT_MAX_RETRIES,
        clock: Callable[[], float] = time.monotonic,
    ) -> None:
        self._send = send
        self._on_acked = on_acked
        self._on_rejected = on_rejected
        self._on_state_change = on_state_change
        self._retry_interval = float(retry_interval_sec)
        self._max_retries = int(max_retries)
        self._clock = clock

        self._lock = threading.RLock()
        self._state = HandshakeState.IDLE
        self._seq = 0
        self._pending_id: Optional[int] = None
        self._pending_seq: Optional[str] = None
        self._attempts = 0
        self._timer: Optional[threading.Timer] = None
        self._allow_override = False

    # ── public state inspection ──────────────────────────────────────────

    @property
    def state(self) -> HandshakeState:
        with self._lock:
            return self._state

    @property
    def confirmed_subject_id(self) -> Optional[int]:
        with self._lock:
            return self._pending_id if self._state == HandshakeState.ACKED else None

    # ── send ──────────────────────────────────────────────────────────────

    def send_subject_id(self, subject_id, allow_override: bool = False) -> bool:
        """
        Begin sending SUBJECT_ID:<n>:<seq>. Returns True on dispatch (validation
        passed and first packet sent), False on validation failure. The actual
        ACK comes asynchronously via handle_inbound().

        allow_override=True sends SUBJECT_ID_OVERRIDE:* — use this only after
        receiving a SUBJECT_ID_REJECT:already_set and the operator has
        explicitly chosen to overwrite.
        """
        if not is_valid_subject_id(subject_id):
            log.error("subject_id %r outside [%d, %d]", subject_id, SUBJECT_ID_MIN, SUBJECT_ID_MAX)
            return False

        with self._lock:
            self._cancel_timer()
            self._pending_id = int(subject_id)
            self._seq += 1
            self._pending_seq = str(self._seq)
            self._attempts = 0
            self._allow_override = bool(allow_override)
            self._set_state(HandshakeState.SENDING)
            self._send_one()
        return True

    def cancel(self) -> None:
        """Abort an in-flight send. State returns to IDLE."""
        with self._lock:
            self._cancel_timer()
            self._pending_id = None
            self._pending_seq = None
            self._attempts = 0
            self._set_state(HandshakeState.IDLE)

    # ── inbound ──────────────────────────────────────────────────────────

    def handle_inbound(self, msg: str) -> bool:
        """
        Feed every inbound message through this. Returns True if the message
        was a SUBJECT_ID_* reply we consumed, False otherwise (so the caller
        can dispatch to other handlers).
        """
        if not msg:
            return False
        if msg.startswith("SUBJECT_ID_ACK:"):
            return self._handle_ack(msg)
        if msg.startswith("SUBJECT_ID_REJECT:"):
            return self._handle_reject(msg)
        return False

    # ── internals ────────────────────────────────────────────────────────

    def _send_one(self) -> None:
        # Caller holds _lock.
        if self._pending_id is None or self._pending_seq is None:
            return
        verb = "SUBJECT_ID_OVERRIDE" if self._allow_override else "SUBJECT_ID"
        wire = f"{verb}:{self._pending_id}:{self._pending_seq}"
        try:
            self._send(wire)
        except Exception:
            log.exception("send failed for %r — will retry", wire)
        self._attempts += 1
        # Start retry timer; cancelled on ACK / REJECT / timeout.
        self._timer = threading.Timer(self._retry_interval, self._on_retry_tick)
        self._timer.daemon = True
        self._timer.start()

    def _on_retry_tick(self) -> None:
        with self._lock:
            if self._state != HandshakeState.SENDING:
                return  # ACKed or rejected while the timer was queued
            if self._attempts >= self._max_retries:
                self._set_state(HandshakeState.TIMEOUT)
                result = HandshakeResult(state=HandshakeState.TIMEOUT,
                                         subject_id=self._pending_id,
                                         reason="no_ack_timeout")
                self._fire_rejected(result)
                return
            self._send_one()

    def _handle_ack(self, msg: str) -> bool:
        # Expect: SUBJECT_ID_ACK:<id>:<seq>
        parts = msg.split(":")
        if len(parts) != 3:
            log.warning("malformed ACK: %r", msg)
            return False
        try:
            ack_id = int(parts[1])
        except ValueError:
            log.warning("non-int id in ACK: %r", msg)
            return False
        ack_seq = parts[2]

        with self._lock:
            if self._pending_seq is None:
                # No outstanding send. Could be an ACK arriving after we cancelled
                # — just absorb it so the caller doesn't double-handle.
                return True
            if ack_seq != self._pending_seq:
                log.info("ignoring ACK for stale seq %s (current %s)", ack_seq, self._pending_seq)
                return True
            if ack_id != self._pending_id:
                # Should not happen — protocol violation. Treat as reject.
                self._cancel_timer()
                result = HandshakeResult(state=HandshakeState.REJECTED,
                                         subject_id=self._pending_id,
                                         reason=f"id_mismatch_ack_{ack_id}")
                self._set_state(HandshakeState.REJECTED)
                self._fire_rejected(result)
                return True

            self._cancel_timer()
            self._set_state(HandshakeState.ACKED)
            confirmed_id = self._pending_id
            # NB: keep _pending_id set — confirmed_subject_id reads from it.

        # Fire callback OUTSIDE the lock so subscribers can call back into us
        # (e.g. UI shows confirmation, then user clicks something that sends
        # another command) without deadlocking.
        if self._on_acked is not None:
            try:
                self._on_acked(confirmed_id)
            except Exception:
                log.exception("on_acked callback raised")
        return True

    def _handle_reject(self, msg: str) -> bool:
        # Expect:
        #   SUBJECT_ID_REJECT:<reason>:<seq>
        #   SUBJECT_ID_REJECT:already_set:<existing_id>:<seq>
        parts = msg.split(":")
        if len(parts) < 3:
            log.warning("malformed REJECT: %r", msg)
            return False

        reason = parts[1]
        existing_id: Optional[int] = None
        if reason == "already_set" and len(parts) >= 4:
            try:
                existing_id = int(parts[2])
            except ValueError:
                existing_id = None
            seq_field = parts[3]
        else:
            seq_field = parts[-1]

        with self._lock:
            if self._pending_seq is None:
                return True
            if seq_field != self._pending_seq:
                log.info("ignoring REJECT for stale seq %s", seq_field)
                return True

            self._cancel_timer()
            self._set_state(HandshakeState.REJECTED)
            result = HandshakeResult(state=HandshakeState.REJECTED,
                                     subject_id=self._pending_id,
                                     reason=reason,
                                     existing_id=existing_id)

        self._fire_rejected(result)
        return True

    def _fire_rejected(self, result: HandshakeResult) -> None:
        if self._on_rejected is not None:
            try:
                self._on_rejected(result)
            except Exception:
                log.exception("on_rejected callback raised")

    def _set_state(self, new_state: HandshakeState) -> None:
        # Caller holds _lock.
        if self._state == new_state:
            return
        self._state = new_state
        cb = self._on_state_change
        if cb is not None:
            # Fire OUTSIDE the lock to avoid reentrancy deadlocks.
            try:
                self._lock.release()
                try:
                    cb(new_state)
                except Exception:
                    log.exception("on_state_change callback raised")
            finally:
                self._lock.acquire()

    def _cancel_timer(self) -> None:
        # Caller holds _lock.
        t = self._timer
        self._timer = None
        if t is not None:
            try:
                t.cancel()
            except Exception:
                pass


# ---------------------------------------------------------------------------
# Smoke test (run with: python -m subject_id_handshake)
# ---------------------------------------------------------------------------

if __name__ == "__main__":  # pragma: no cover
    logging.basicConfig(level=logging.DEBUG, format="%(levelname)s %(name)s | %(message)s")

    sent = []

    def fake_send(msg: str) -> None:
        print("→ wire:", msg)
        sent.append(msg)

    def on_acked(sid: int) -> None:
        print("✓ acked:", sid)

    def on_rejected(r: HandshakeResult) -> None:
        print("✕ rejected:", r)

    def on_state(s: HandshakeState) -> None:
        print("⇢ state:", s.value)

    h = SubjectIdHandshake(
        send=fake_send,
        on_acked=on_acked,
        on_rejected=on_rejected,
        on_state_change=on_state,
        retry_interval_sec=0.05,
        max_retries=2,
    )

    # Happy path
    assert h.send_subject_id(42) is True
    h.handle_inbound(f"SUBJECT_ID_ACK:42:{h._pending_seq}")
    assert h.confirmed_subject_id == 42

    # Validation
    assert h.send_subject_id(0) is False
    assert h.send_subject_id("abc") is False

    # already_set rejection followed by override
    h2 = SubjectIdHandshake(send=fake_send, retry_interval_sec=0.05, max_retries=2)
    h2.send_subject_id(7)
    h2.handle_inbound(f"SUBJECT_ID_REJECT:already_set:5:{h2._pending_seq}")
    assert h2.state == HandshakeState.REJECTED
    h2.send_subject_id(7, allow_override=True)
    assert sent[-1].startswith("SUBJECT_ID_OVERRIDE:7:")

    # Timeout
    h3 = SubjectIdHandshake(send=fake_send, retry_interval_sec=0.02, max_retries=2)
    h3.send_subject_id(99)
    time.sleep(0.2)
    assert h3.state == HandshakeState.TIMEOUT, h3.state

    print("\nALL SMOKE TESTS PASSED")
