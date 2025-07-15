using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

public class GrapplingHookVerlet : MonoBehaviour
{
    [Header("Rope Settings")]
    [SerializeField] private int segmentCount = 15;
    [SerializeField] private float segmentLength = 0.5f;
    [SerializeField] private int constraintIterations = 5;
    [SerializeField] private float gravity = -9.8f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private float ropeStiffness = 50f;
    [SerializeField] private LayerMask grappleMask;

    [Header("Swing Settings")]
    [SerializeField] private float swingForce = 3f;
    [SerializeField] private bool allowSwingControl = true;

    [Header("Rope Attachment")]
    [SerializeField] public Transform ropeStartShootPoint;

    [Header("References")]
    [SerializeField] private GameUI gameUI;
    
    private Camera _camera;
    
    private Vector3 _screenCenter;

    private List<VerletSegment> _segments = new List<VerletSegment>();
    private bool _ropeActive;
    private Vector3 _grapplePoint;

    private Rigidbody _rigidbody;
    
    private NativeArray<VerletSegment> nativeSegments;
    private bool nativeArraysInitialized;
    
    private Vector3 lastRopeDir = Vector3.zero;
    private float lastAngularVelocity;
    private float angularAcceleration;

    public List<VerletSegment> Segments => _segments;
    public NativeArray<VerletSegment> NativeSegments => nativeSegments;
    public bool NativeArraysInitialized => nativeArraysInitialized;


    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        
        _camera = Camera.main;
    }

    private void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        
        _screenCenter = new Vector3(Screen.width / 2, Screen.height / 2);
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryShootRope();

        if (Input.GetMouseButtonDown(1))
            DetachRope();
    }

    private void FixedUpdate()
    {
        if (_ropeActive && nativeArraysInitialized)
        {

            var simulateJob = new SimulateJob
            (
                Time.fixedDeltaTime,
                new Vector3(0, gravity, 0),
                nativeSegments
            );

            JobHandle simulateHandle = simulateJob.Schedule(nativeSegments.Length, 64);
            
            var constraintsJob = new ApplyConstraintsJob
            (
                segmentCount,
                segmentLength,
                ropeStartShootPoint != null ? ropeStartShootPoint.position : transform.position,
                _grapplePoint,
                nativeSegments,
                constraintIterations
            );

            JobHandle constraintsHandle = constraintsJob.Schedule(simulateHandle);
            constraintsHandle.Complete();

            _segments.Clear();
            for (int i = 0; i < nativeSegments.Length; i++)
                _segments.Add(nativeSegments[i]);

            UpdateAngularMotion(); 

            ApplySwingPhysics();
        }
    }
    
    private void TryShootRope()
    {
        Ray ray = _camera.ScreenPointToRay(_screenCenter);
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, grappleMask))
        {
            _grapplePoint = hit.point;
            InitRope(_grapplePoint);
        }
    }

    private void DetachRope()
    {
        _ropeActive = false;
        _segments.Clear();

        if (nativeArraysInitialized)
        {
            if (nativeSegments.IsCreated)
                nativeSegments.Dispose();
            nativeArraysInitialized = false;
        }
    }

    private void InitRope(Vector3 targetPoint)
    {
        if (nativeArraysInitialized)
        {
            if (nativeSegments.IsCreated)
                nativeSegments.Dispose();
            nativeArraysInitialized = false;
        }

        nativeSegments = new NativeArray<VerletSegment>(segmentCount, Allocator.Persistent);

        Vector3 startPos = transform.position;
        Vector3 dir = (targetPoint - startPos).normalized;

        for (int i = 0; i < segmentCount; i++)
        {
            Vector3 pos = startPos + dir * (segmentLength * i);
            nativeSegments[i] = new VerletSegment(pos);
        }

        _ropeActive = true;
        _grapplePoint = targetPoint;
        nativeArraysInitialized = true;
        
        _segments.Clear();
        for (int i = 0; i < nativeSegments.Length; i++)
            _segments.Add(nativeSegments[i]);

        lastRopeDir = Vector3.zero;
        lastAngularVelocity = 0f;
    }

    private void ApplySwingPhysics()
    {
        Vector3 toGrapple = _grapplePoint - transform.position;
        float currentLength = toGrapple.magnitude;
        float maxLength = segmentCount * segmentLength;

        Vector3 dir = toGrapple.normalized;

        if (currentLength > maxLength)
        {
            Vector3 velocityAlongDir = Vector3.Project(_rigidbody.linearVelocity, dir);
            Vector3 correctedVelocity = _rigidbody.linearVelocity - velocityAlongDir;
            _rigidbody.linearVelocity = correctedVelocity;

            Vector3 pullForce = dir * (currentLength - maxLength) * ropeStiffness;
            _rigidbody.AddForce(pullForce, ForceMode.Acceleration);
        }

        if (allowSwingControl)
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Transform camTransform = Camera.main.transform;

            Vector3 camForward = camTransform.forward;
            camForward.y = 0;
            camForward.Normalize();

            Vector3 camRight = camTransform.right;
            camRight.y = 0;
            camRight.Normalize();

            Vector3 swingForward = Vector3.ProjectOnPlane(camForward, dir).normalized;
            Vector3 swingRight = Vector3.ProjectOnPlane(camRight, dir).normalized;

            _rigidbody.AddForce(swingRight * horizontal * swingForce, ForceMode.Acceleration);
            _rigidbody.AddForce(swingForward * vertical * swingForce, ForceMode.Acceleration);
        }
    }
    
    private void UpdateAngularMotion()
    {
        if (_segments.Count < 2)
            return;

        Vector3 start = _segments[0].position;
        Vector3 end = _segments[_segments.Count - 1].position;
        Vector3 currentDir = (end - start).normalized;

        if (lastRopeDir != Vector3.zero)
        {
            float angle = Vector3.Angle(lastRopeDir, currentDir);
            Vector3 cross = Vector3.Cross(lastRopeDir, currentDir);
            float sign = Mathf.Sign(Vector3.Dot(cross, Vector3.up));
            float angleSigned = angle * sign;

            float newAngularVelocity = angleSigned / Time.fixedDeltaTime;
            angularAcceleration = (newAngularVelocity - lastAngularVelocity) / Time.fixedDeltaTime;

            lastAngularVelocity = newAngularVelocity;
        }

        lastRopeDir = currentDir;
        
        gameUI.ShowCurrentAngularAcceleration(angularAcceleration);
    }


    private void OnDestroy()
    {
        if (nativeArraysInitialized)
        {
            if (nativeSegments.IsCreated)
                nativeSegments.Dispose();
            nativeArraysInitialized = false;
        }
    }

    [BurstCompile]
    private struct SimulateJob : IJobParallelFor
    {
        public float _dt;
        public Vector3 _gravity;
        public NativeArray<VerletSegment> _segments;


        public SimulateJob(float dt,
            Vector3 gravity,
            NativeArray<VerletSegment> segments)
        {
            _dt = dt;
            _gravity = gravity;
            _segments = segments;
        }
        
        public void Execute(int index)
        {
            VerletSegment seg = _segments[index];
            Vector3 velocity = seg.position - seg.previousPosition;
            seg.previousPosition = seg.position;
            seg.position += velocity;
            seg.position += _gravity * _dt * _dt;
            _segments[index] = seg;
        }
    }

    [BurstCompile]
    private struct ApplyConstraintsJob : IJob
    {
        public int _segmentCount;
        public float _segmentLength;
        public Vector3 _ropeAttachPosition;
        public Vector3 _grapplePoint;
        public NativeArray<VerletSegment> _segments;
        public int _constraintIterations;
        

        public ApplyConstraintsJob(int segmentCount, 
            float segmentLength, 
            Vector3 ropeAttachPosition, 
            Vector3 grapplePoint, 
            NativeArray<VerletSegment> segments, 
            int constraintIterations)
        {
            _segmentCount = segmentCount;
            _segmentLength = segmentLength;
            _ropeAttachPosition = ropeAttachPosition;
            _grapplePoint = grapplePoint;
            _segments = segments;
            _constraintIterations = constraintIterations;
        }

        public void Execute()
        {
            if (_segmentCount == 0) return;

            VerletSegment startSeg = _segments[0];
            startSeg.position = _ropeAttachPosition;
            _segments[0] = startSeg;

            VerletSegment endSeg = _segments[_segmentCount - 1];
            endSeg.position = _grapplePoint;
            _segments[_segmentCount - 1] = endSeg;

            for (int k = 0; k < _constraintIterations; k++)
            {
                for (int i = 0; i < _segmentCount - 1; i++)
                {
                    VerletSegment segA = _segments[i];
                    VerletSegment segB = _segments[i + 1];

                    Vector3 delta = segB.position - segA.position;
                    float dist = delta.magnitude;
                    float error = dist - _segmentLength;
                    Vector3 correction = delta.normalized * (error * 0.5f);

                    if (i != 0)
                        segA.position += correction;

                    if (i != _segmentCount - 2)
                        segB.position -= correction;

                    _segments[i] = segA;
                    _segments[i + 1] = segB;
                }
            }
        }
    }

    public struct VerletSegment
    {
        public Vector3 position;
        public Vector3 previousPosition;

        public VerletSegment(Vector3 pos)
        {
            position = pos;
            previousPosition = pos;
        }
    }
}