using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed;
    [SerializeField] private float jumpForce;
    [SerializeField] private Transform head;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance;

    private Rigidbody _rigidbody;
    private Transform _transform;

    private NativeArray<Vector3> _moveDirections;
    private JobHandle _movementJobHandle;

    private bool _isGrounded;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _transform = transform;
        
        _moveDirections = new NativeArray<Vector3>(1, Allocator.Persistent);
    }

    private void FixedUpdate()
    {
        _isGrounded = Physics.Raycast(_transform.position, Vector3.down, groundCheckDistance + 0.1f, groundMask);
        
        var job = new PlayerMovementJob
        (
            Input.GetAxis("Horizontal"),
            Input.GetAxis("Vertical"),
            head.right,
            head.forward,
            speed,
            Time.fixedDeltaTime,
            _transform.position,
            _moveDirections
        );

        _movementJobHandle = job.Schedule(1, 1);
        _movementJobHandle.Complete();
        
        Vector3 move = _moveDirections[0];
        _rigidbody.MovePosition(move);
        
        if (Input.GetKey(KeyCode.Space) && _isGrounded)
        {
            _rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
    
    private void OnDestroy()
    {
        if (_moveDirections.IsCreated)
            _moveDirections.Dispose();
    }

    [BurstCompile]
    private struct PlayerMovementJob : IJobParallelFor
    {
        private float _horizontal;
        private float _vertical;
        
        private Vector3 _headRight;
        private Vector3 _headForward;
        
        private float _speed;
        private float _deltaTime;
        
        private Vector3 _currentPosition;

        private NativeArray<Vector3> _outMoveDirection;
        
        
        public PlayerMovementJob(
            float horizontal, 
            float vertical,
            Vector3 headRight,
            Vector3 headForward,
            float speed,
            float deltaTime,
            Vector3 currentPosition,
            NativeArray<Vector3> outMoveDirection
            )
        {
            _horizontal = horizontal;
            _vertical = vertical;
            _headRight = headRight;
            _headForward = headForward;
            _speed = speed;
            _deltaTime = deltaTime;
            _currentPosition = currentPosition;
            _outMoveDirection = outMoveDirection;
        }

        public void Execute(int index)
        {
            Vector3 moveDir = _horizontal * _headRight + _vertical * _headForward;
            if (moveDir.sqrMagnitude > 1f)
                moveDir.Normalize();

            Vector3 newPos = _currentPosition + moveDir * _speed * _deltaTime;
            _outMoveDirection[index] = newPos;
        }
    }
}
