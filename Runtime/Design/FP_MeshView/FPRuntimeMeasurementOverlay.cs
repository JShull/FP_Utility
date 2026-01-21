namespace FuzzPhyte.Utility
{
    using System.Collections;
    using UnityEngine;

    public sealed class FPRuntimeMeasurementOverlay : MonoBehaviour
    {
        public static FPRuntimeMeasurementOverlay Active { get; private set; }

        [Header("Materials")]
        [SerializeField] private Material pointMat;
        [SerializeField] private Material lineMat;
        [SerializeField] private UnitOfMeasure _units;
        public UnitOfMeasure Units=> _units;
        public Material PointMat => pointMat;
        public Material LineMat => lineMat;

        [Header("Settings")]
        public float PointSize = 0.01f;
        public float PointOpacity = 1f;
        public Color PointColor = Color.cyan;

        public float LineWidthWorld = 0.01f;
        public float LineOpacity = 1f;
        public Color LineColor = Color.yellow;

        private ComputeBuffer _points;
        private ComputeBuffer _linePoints;
        private bool _hasMeasurement;
        private Vector3 _a, _b;

        public bool HasMeasurement => _hasMeasurement;
        public Vector3 A => _a;
        public Vector3 B => _b;

        private readonly Vector3[] _twoPoints = new Vector3[2];

        public ComputeBuffer PointsBuffer => _points;
        public ComputeBuffer LineBuffer => _linePoints;
        private bool _commandBuffersInitialized = false;

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        void Awake()
        {
            if (!_commandBuffersInitialized)
            {
                InitializeCommandBuffers();
                _commandBuffersInitialized = true;
            }
        }

        void OnDestroy()
        {
            _points?.Dispose();
            _linePoints?.Dispose();
        }

        public void SetMeasurement(Vector3 a, Vector3 b, bool enabled, UnitOfMeasure units)
        {
            _units = units;
            _a = a; _b = b; _hasMeasurement = enabled;
            _twoPoints[0] = _a;
            _twoPoints[1] = _b;
            if (_points != null)
            {
                _points.SetData(_twoPoints);
            }
            else
            {
                _points = new ComputeBuffer(2, sizeof(float) * 3);
            }
            if (_linePoints != null)
            {
                _linePoints.SetData(_twoPoints);
            }
            else
            {
                _linePoints = new ComputeBuffer(2, sizeof(float) * 3);
            }
           
        }
        private void InitializeCommandBuffers()
        {
            _points = new ComputeBuffer(2, sizeof(float) * 3);
            _linePoints = new ComputeBuffer(2, sizeof(float) * 3);
            // initialize safe values
            SetMeasurement(Vector3.zero, Vector3.zero, false,UnitOfMeasure.Meter);
        }
        public void ClearMeasurement()
        {
            if (!_commandBuffersInitialized)
            {
                InitializeCommandBuffers();
                _commandBuffersInitialized = true;
                return;
            }
            SetMeasurement(Vector3.zero, Vector3.zero, false, UnitOfMeasure.Meter);
        }
    }
}
