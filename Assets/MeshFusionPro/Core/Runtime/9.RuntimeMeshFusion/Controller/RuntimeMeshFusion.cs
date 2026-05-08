using System;
using System.Collections.Generic;
using UnityEngine;


namespace NGS.MeshFusionPro
{
    public partial class RuntimeMeshFusion : MonoBehaviour
    {
        private static List<RuntimeMeshFusion> _Instances;

        public event Action<CombinedObject> onStaticCombinedObjectCreated;
        public event Action<DynamicCombinedObject> onDynamicCombinedObjectCreated;
        public event Action<SkinnedCombinedObject> onSkinnedCombinedObjectCreated;
        public event Action<CombinedLODGroup> onCombinedLODGroupCreated;

        public int ControllerIndex
        {
            get
            {
                return _controllerIndex;
            }
            set
            {
                if (!Application.isPlaying)
                    _controllerIndex = value;
            }
        }
        public bool DrawGizmo
        {
            get
            {
                return _drawGizmo;
            }
            set
            {
                _drawGizmo = value;
            }
        }
        public int CellSize
        {
            get
            {
                return _cellSize;
            }
            set
            {
                if (Application.isPlaying)
                    return;

                _cellSize = Mathf.Max(1, value);
            }
        }
        public bool LimitVertices
        {
            get
            {
                return _maxVerticesPerObject <= 65535;
            }
            set
            {
                if (Application.isPlaying)
                    return;

                if (value)
                {
                    _maxVerticesPerObject = 65535;
                }
                else
                {
                    _maxVerticesPerObject = int.MaxValue;
                }
            }
        }
        public int BonesLimit
        {
            get
            {
                return _maxBonesPerObject;
            }
            set
            {
                _maxBonesPerObject = Mathf.Max(value, 1);
            }
        }
        public MeshType MeshType
        {
            get
            {
                return _meshType;
            }
            set
            {
                if (!Application.isPlaying)
                    _meshType = value;
            }
        }
        public MoveMethod MoveMethod
        {
            get
            {
                return _moveMethod;
            }
            set
            {
                if (!Application.isPlaying)
                    _moveMethod = value;
            }
        }

        [SerializeField]
        private int _controllerIndex = 0;

        [SerializeField]
        private bool _drawGizmo;

        [SerializeField]
        private int _cellSize = 80;

        [SerializeField]
        private int _maxVerticesPerObject = 65535;

        [SerializeField]
        private int _maxBonesPerObject = 1000;

        [SerializeField]
        private MeshType _meshType = MeshType.Standard;

        [SerializeField]
        private MoveMethod _moveMethod = MoveMethod.Jobs;

        private CombineTree _combineTree;
        private bool _sourceAdded;

        private BinaryTreeDrawer<ICombineSource> _treeDrawer;


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReloadDomains()
        {
            DomainReloader.Reload();
        }


        private void Awake()
        {
            if (_Instances == null)
                _Instances = new List<RuntimeMeshFusion>();

            _Instances.Add(this);

            ICombinedMeshFactory factory = new CombinedMeshFactory(_meshType, CombineMethod.Simple, _moveMethod);

            _combineTree = new CombineTree(factory, _cellSize, _maxVerticesPerObject, _maxBonesPerObject);
            _treeDrawer = new BinaryTreeDrawer<ICombineSource>();

            Transform parent = new GameObject("CombinedObjects").transform;

            _combineTree.onStaticCombinedObjectCreated += (c) => { c.transform.parent = parent; onStaticCombinedObjectCreated?.Invoke(c); };
            _combineTree.onDynamicCombinedObjectCreated += (c) => { c.transform.parent = parent; onDynamicCombinedObjectCreated?.Invoke(c); };
            _combineTree.onSkinnedCombinedObjectCreated += (c) => { c.transform.parent = parent; onSkinnedCombinedObjectCreated?.Invoke(c); };
            _combineTree.onCombinedLODGroupCreated += (c) => { c.transform.parent = parent; onCombinedLODGroupCreated?.Invoke(c); };
        }

        private void Update()
        {
            if (_sourceAdded)
            {
                _combineTree.Combine();
                _sourceAdded = false;
            }
        }

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !_drawGizmo)
                return;

            if (_combineTree != null && _combineTree.Root != null)
                _treeDrawer.DrawGizmo(_combineTree.Root, Color.white);
        }

        private void OnDestroy()
        {
            _Instances.Remove(this);
        }


        public static RuntimeMeshFusion FindByIndex(int index)
        {
            for (int i = 0; i < _Instances.Count; i++)
            {
                RuntimeMeshFusion controller = _Instances[i];

                if (controller.ControllerIndex == index)
                    return controller;
            }

            throw new KeyNotFoundException("MeshFusionController with index : " + index + " not found");
        }


        public void AddSource(ICombineSource source)
        {
            _combineTree.Add(source);

            _sourceAdded = true;
        }

        public void RemoveSource(ICombineSource source)
        {
            _combineTree.Remove(source);
        }


        private static partial class DomainReloader { }
    }
}
