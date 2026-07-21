using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class VehicleController : MonoBehaviour
{
    [Header("Araç Özellikleri")]
    public float motorForce = 1500f;
    public float maxSpeed = 120f;
    public float maxReverseSpeed = 30f;
    public float maxSteerAngle = 35f;
    public float brakeForce = 3000f;
    public float engineBrakeForce = 300f;

    [Header("Gelişmiş Yol Tutuşu (GTA Fiziği)")]
    public float steerSpeed = 5f;
    public float downForce = 100f;
    public float driftStiffness = 0.3f;
    [Tooltip("Hızlandıkça araba daha az döner, böylece savrulmaz")]
    public float highSpeedSteerAngle = 10f;
    [Tooltip("Virajlarda arabanın yatmasını ve kaymasını engeller")]
    public float antiRollForce = 4000f;

    [Header("Fizik Ayarları")]
    public Transform centerOfMass;
    public WheelCollider frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider;
    public Transform frontLeftMesh, frontRightMesh, rearLeftMesh, rearRightMesh;

    public bool isPlayerInside = false;

    public float currentSpeed { get; private set; }
    public bool IsAccelerating { get; private set; }
    public bool IsBraking { get; private set; }
    public bool IsReversing { get; private set; }
    public bool IsHandbrakeActive { get; private set; }

    private float currentMotorForce, currentSteerAngle, currentBrakeForce;
    private float originalRearStiffness;
    private Quaternion[] initialWheelRotations = new Quaternion[4];
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (centerOfMass != null) rb.centerOfMass = centerOfMass.localPosition;

        if (frontLeftMesh != null) initialWheelRotations[0] = frontLeftMesh.localRotation;
        if (frontRightMesh != null) initialWheelRotations[1] = frontRightMesh.localRotation;
        if (rearLeftMesh != null) initialWheelRotations[2] = rearLeftMesh.localRotation;
        if (rearRightMesh != null) initialWheelRotations[3] = rearRightMesh.localRotation;

        originalRearStiffness = rearLeftCollider.sidewaysFriction.stiffness;
    }

    private void FixedUpdate()
    {
        if (Keyboard.current == null) return;

        CalculateSpeedAndStates();

        if (!isPlayerInside)
        {
            ApplyHandbrake(true);
            UpdateWheels();
            return;
        }

        HandleInput();
        HandleMotor();
        HandleSteering();
        ApplyDownForce();

        // Arabanın yola yapışmasını sağlayan Denge Çubuğu
        ApplyAntiRollBar(frontLeftCollider, frontRightCollider);
        ApplyAntiRollBar(rearLeftCollider, rearRightCollider);

        UpdateWheels();
    }

    private void CalculateSpeedAndStates()
    {
        currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        float forwardVelocity = transform.InverseTransformDirection(rb.linearVelocity).z;

        float vertical = isPlayerInside ? (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0) : 0;
        IsHandbrakeActive = isPlayerInside && Keyboard.current.spaceKey.isPressed;

        IsAccelerating = false;
        IsBraking = false;
        IsReversing = false;

        if (vertical > 0)
        {
            if (forwardVelocity < -0.5f) IsBraking = true;
            else IsAccelerating = true;
        }
        else if (vertical < 0)
        {
            if (forwardVelocity > 0.5f) IsBraking = true;
            else { IsAccelerating = true; IsReversing = true; }
        }

        if (IsHandbrakeActive) IsBraking = true;
    }

    private void HandleInput()
    {
        float vertical = (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0);
        float horizontal = (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0);

        // HIZA BAĞLI DİREKSİYON (Savrulmayı çözer)
        float speedRatio = currentSpeed / maxSpeed;
        float activeMaxSteer = Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedRatio);

        float targetSteerAngle = horizontal * activeMaxSteer;
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetSteerAngle, Time.fixedDeltaTime * steerSpeed);

        if (!IsBraking) currentMotorForce = vertical * motorForce;
        else currentMotorForce = 0f;

        if (vertical == 0 && !IsBraking) currentBrakeForce = engineBrakeForce;
        else if (IsBraking && !IsHandbrakeActive) currentBrakeForce = brakeForce;
        else currentBrakeForce = 0f;
    }

    private void HandleMotor()
    {
        ApplyHandbrake(IsHandbrakeActive);

        float activeMaxSpeed = IsReversing ? maxReverseSpeed : maxSpeed;
        float appliedMotorForce = currentSpeed < activeMaxSpeed ? currentMotorForce : 0f;

        frontLeftCollider.motorTorque = appliedMotorForce;
        frontRightCollider.motorTorque = appliedMotorForce;
        rearLeftCollider.motorTorque = appliedMotorForce;
        rearRightCollider.motorTorque = appliedMotorForce;

        if (!IsHandbrakeActive)
        {
            frontLeftCollider.brakeTorque = currentBrakeForce;
            frontRightCollider.brakeTorque = currentBrakeForce;
            rearLeftCollider.brakeTorque = currentBrakeForce;
            rearRightCollider.brakeTorque = currentBrakeForce;
        }
    }

    private void ApplyHandbrake(bool active)
    {
        WheelFrictionCurve frictionL = rearLeftCollider.sidewaysFriction;
        WheelFrictionCurve frictionR = rearRightCollider.sidewaysFriction;

        if (active)
        {
            rearLeftCollider.brakeTorque = brakeForce;
            rearRightCollider.brakeTorque = brakeForce;
            frontLeftCollider.brakeTorque = 0f;
            frontRightCollider.brakeTorque = 0f;

            frictionL.stiffness = driftStiffness;
            frictionR.stiffness = driftStiffness;
        }
        else
        {
            frictionL.stiffness = originalRearStiffness;
            frictionR.stiffness = originalRearStiffness;
        }

        rearLeftCollider.sidewaysFriction = frictionL;
        rearRightCollider.sidewaysFriction = frictionR;
    }

    private void HandleSteering()
    {
        if (currentSteerAngle > 0)
        {
            frontRightCollider.steerAngle = currentSteerAngle;
            frontLeftCollider.steerAngle = currentSteerAngle * 0.8f;
        }
        else if (currentSteerAngle < 0)
        {
            frontLeftCollider.steerAngle = currentSteerAngle;
            frontRightCollider.steerAngle = currentSteerAngle * 0.8f;
        }
        else
        {
            frontLeftCollider.steerAngle = 0;
            frontRightCollider.steerAngle = 0;
        }
    }

    private void ApplyDownForce() { rb.AddForce(-transform.up * rb.linearVelocity.magnitude * downForce); }

    // Denge Çubuğu: Arabanın virajda takla atmasını ve boşa kaymasını önler
    private void ApplyAntiRollBar(WheelCollider left, WheelCollider right)
    {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        bool groundedL = left.GetGroundHit(out hit);
        if (groundedL) travelL = (-left.transform.InverseTransformPoint(hit.point).y - left.radius) / left.suspensionDistance;

        bool groundedR = right.GetGroundHit(out hit);
        if (groundedR) travelR = (-right.transform.InverseTransformPoint(hit.point).y - right.radius) / right.suspensionDistance;

        float antiRollVal = (travelL - travelR) * antiRollForce;

        if (groundedL) rb.AddForceAtPosition(left.transform.up * -antiRollVal, left.transform.position);
        if (groundedR) rb.AddForceAtPosition(right.transform.up * antiRollVal, right.transform.position);
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh, 0);
        UpdateSingleWheel(frontRightCollider, frontRightMesh, 1);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh, 2);
        UpdateSingleWheel(rearRightCollider, rearRightMesh, 3);
    }

    private void UpdateSingleWheel(WheelCollider col, Transform mesh, int index)
    {
        if (mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot * initialWheelRotations[index];
    }
}