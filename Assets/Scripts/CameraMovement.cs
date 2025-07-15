using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Range(0,100)]
    [SerializeField] private int sensitivity;
    [Range(0,90)]
    [SerializeField] private float maxLookAngle;

    private float _horizontal;
    private float _vertical;

    private Quaternion _fixedRotation;
    private Quaternion _previousRotation;

    private NativeArray<Quaternion> _rotationResult;
    private JobHandle _rotationJobHandle;

    private void Start()
    {
        _fixedRotation = transform.localRotation;
        _previousRotation = _fixedRotation;

        _rotationResult = new NativeArray<Quaternion>(1, Allocator.Persistent);
    }

    private void FixedUpdate()
    {
        _previousRotation = _fixedRotation;

        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime;

        _horizontal += mouseX;
        _vertical -= mouseY;
        _vertical = Mathf.Clamp(_vertical, -maxLookAngle, maxLookAngle);

        var job = new CameraRotationJob(_vertical, _horizontal, _rotationResult);

        _rotationJobHandle = job.Schedule();
        _rotationJobHandle.Complete();

        _fixedRotation = _rotationResult[0];
    }

    private void LateUpdate()
    {
        float interpolationFactor = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        interpolationFactor = Mathf.Clamp01(interpolationFactor);

        Quaternion interpolatedRotation = Quaternion.Slerp(_previousRotation, _fixedRotation, interpolationFactor);
        transform.localRotation = interpolatedRotation;
    }
    
    private void OnDestroy()
    {
        if (_rotationResult.IsCreated)
            _rotationResult.Dispose();
    }

    [BurstCompile]
    private struct CameraRotationJob : IJob
    {
        private float _vertical;
        private float _horizontal;

        private NativeArray<Quaternion> _outRotation;

        public CameraRotationJob(
            float vertical, 
            float horizontal, 
            NativeArray<Quaternion> outRotation)
        {
            _vertical = vertical;
            _horizontal = horizontal;
            _outRotation = outRotation;
        }

        public void Execute()
        {
            _outRotation[0] = Quaternion.Euler(_vertical, _horizontal, 0f);
        }
    }
}
