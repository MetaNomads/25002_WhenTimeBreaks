using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace NGS.MeshFusionPro
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SourceTracker))]
    public class SourceTrackerEditor : Editor
    {
        protected new SourceTracker target
        {
            get
            {
                return base.target as SourceTracker;
            }
        }

        private SerializedProperty _trackingDestroyProp;
        private SerializedProperty _trackingTargetProp;
        private SerializedProperty _trackingStrategyProp;
        private SerializedProperty _disableWhenIdleProp;
        private SerializedProperty _maxIdleTimeProp;
        private SerializedProperty _wakeUpWhenCollisionProp;

        private void OnEnable()
        {
            _trackingDestroyProp = serializedObject.FindAutoProperty(nameof(target.TrackingDestroy));
            _trackingTargetProp = serializedObject.FindProperty("_trackingTarget");
            _trackingStrategyProp = serializedObject.FindProperty("_trackingStrategy");
            _disableWhenIdleProp = serializedObject.FindAutoProperty(nameof(target.DisableWhenIdle));
            _maxIdleTimeProp = serializedObject.FindAutoProperty(nameof(target.MaxIdleTime));
            _wakeUpWhenCollisionProp = serializedObject.FindAutoProperty(nameof(target.WakeUpWhenCollision));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(_trackingTargetProp);

            if (!_trackingTargetProp.hasMultipleDifferentValues)
            {
                if (target.TrackingTarget != TrackingTarget.None)
                {
                    if (target.TrackingTarget != TrackingTarget.Transform)
                    {
                        DrawTrackingStrategyOptions();
                    }

                    DrawTrackingProperties();
                }
            }

            EditorGUILayout.PropertyField(_trackingDestroyProp);

            if (EditorGUI.EndChangeCheck())
            {
                ApplyChanges();

                EditorUtility.SetDirty(target);
            }
        }

        private void DrawTrackingProperties()
        {
            EditorGUILayout.PropertyField(_disableWhenIdleProp);

            if (!_disableWhenIdleProp.boolValue || _disableWhenIdleProp.hasMultipleDifferentValues)
                return;

            EditorGUILayout.PropertyField(_maxIdleTimeProp);
            EditorGUILayout.PropertyField(_wakeUpWhenCollisionProp);
        }

        private void DrawTrackingStrategyOptions()
        {
            if (target.TrackingStrategy is RigidbodyTrackingStrategy)
            {
                SerializedProperty velocityThresholdProp = _trackingStrategyProp.FindPropertyRelative("_velocityThreshold");
                SerializedProperty angularVelocityThresholdProp = _trackingStrategyProp.FindPropertyRelative("_angularVelocityThreshold");

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(velocityThresholdProp);
                EditorGUILayout.PropertyField(angularVelocityThresholdProp);

                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var currentTarget in targets)
                    {
                        ((currentTarget as SourceTracker).TrackingStrategy as RigidbodyTrackingStrategy).VelocityThreshold = velocityThresholdProp.floatValue;
                        ((currentTarget as SourceTracker).TrackingStrategy as RigidbodyTrackingStrategy).AngularVelocityThreshold = angularVelocityThresholdProp.floatValue;
                    }
                }
            }
            else if (target.TrackingStrategy is SkinnedMeshTrackingStrategy)
            {
                SerializedProperty positionThresholdProp = _trackingStrategyProp.FindPropertyRelative("_positionThreshold");

                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(positionThresholdProp);

                if (EditorGUI.EndChangeCheck())
                {
                    foreach (var currentTarget in targets)
                    {
                        ((currentTarget as SourceTracker).TrackingStrategy as SkinnedMeshTrackingStrategy).PositionThreshold = positionThresholdProp.floatValue;
                    }
                }
            }
        }

        private void ApplyChanges()
        {
            foreach (var currentTarget in targets)
            {
                SourceTracker target = currentTarget as SourceTracker;

                if (!_trackingTargetProp.hasMultipleDifferentValues)
                {
                    TrackingTarget trackingTarget = (TrackingTarget)_trackingTargetProp.enumValueIndex;

                    target.TrackingTarget = trackingTarget;

                    if (trackingTarget != TrackingTarget.None)
                    {
                        if (!_disableWhenIdleProp.hasMultipleDifferentValues)
                        {
                            target.DisableWhenIdle = _disableWhenIdleProp.boolValue;

                            if (target.DisableWhenIdle)
                            {
                                if (!_maxIdleTimeProp.hasMultipleDifferentValues)
                                    target.MaxIdleTime = _maxIdleTimeProp.floatValue;

                                if (!_wakeUpWhenCollisionProp.hasMultipleDifferentValues)
                                    target.WakeUpWhenCollision = _wakeUpWhenCollisionProp.boolValue;
                            }
                        }
                    }
                }

                if (!_trackingDestroyProp.hasMultipleDifferentValues)
                    target.TrackingDestroy = _trackingDestroyProp.boolValue;
            }
        }
    }
}
