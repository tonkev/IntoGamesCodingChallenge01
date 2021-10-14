using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum PlayerState
{
  Grounded,
  Jumping,
  Falling
}

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{

  public float mouseSensitivity;
  public float analogSensitivity;
  public bool flipAnalogY;

  public float runAccel;
  public float runDeaccel;
  public float runSpeedMax;

  public float gravity;
  public float airAccel;
  public float jumpSpeed;
  // time player can still jump despite no longer being on the ground
  public float coyoteTime;
  // time after a jump press that player will jump once they land on ground
  public float reverseCoyoteTime;

  public uint numGroundRays;
  public float groundRayRadius;

  public Transform equippedTransform;

  IEquippable equipped;

  Vector3 moveStep = Vector3.zero;

  Vector3 spawnPos = Vector3.zero;

  float tiltAngle = 0f;
  float coyoteTimestamp = 0f;
  float reverseCoyoteTimestamp = 0f;
  PlayerState state = PlayerState.Falling;

  public Rigidbody rb;
  public Transform cameraTransform;
  Transform panTransform;
  Transform tiltTransform;
  Transform bodyTransform;
  Animator animator;
  LayerMask groundMask;

  void Start()
  {
    rb = GetComponent<Rigidbody>();
    cameraTransform = transform.parent.Find("Camera");
    panTransform = transform.Find("Pan");
    tiltTransform = transform.Find("Pan/Tilt");
    bodyTransform = transform.Find("Body");
    animator = bodyTransform.Find("Body").GetComponent<Animator>();

    equipped = equippedTransform.GetComponent<IEquippable>();
    equipped.Equip(this);

    spawnPos = transform.position;

    groundMask = LayerMask.GetMask("Ground");
  }

  void Update()
  {
    if (Input.GetMouseButtonDown(0))
    {
      Cursor.lockState = CursorLockMode.Locked;
    }

    if (Input.GetKeyDown(KeyCode.Escape))
    {
      Cursor.lockState = CursorLockMode.None;
    }

    Vector2 lookStep = Vector2.zero;
    if (CursorLockMode.Locked == Cursor.lockState)
    {
      lookStep.x += Input.GetAxisRaw("Mouse X") * mouseSensitivity;
      lookStep.y += Input.GetAxisRaw("Mouse Y") * mouseSensitivity;
    }
    lookStep.x += Input.GetAxis("Analog X") * analogSensitivity;
    lookStep.y += Input.GetAxis("Analog Y") * analogSensitivity * (flipAnalogY ? 1 : -1);
    lookStep *= Time.deltaTime;

    tiltAngle = Mathf.Clamp(tiltAngle - lookStep.y, -70f, 70f);
    tiltTransform.localRotation = Quaternion.Euler(tiltAngle, 0f, 0f);

    panTransform.Rotate(Vector3.up * lookStep.x);

    // project camera normals so that movement speed is consistent
    // whether we are looking at player from behind or above
    Vector3 flatForward = new Vector3(cameraTransform.forward.x, 0f, cameraTransform.forward.z);
    Vector3 flatRight = new Vector3(cameraTransform.right.x, 0f, cameraTransform.right.z);
    moveStep = flatForward * Input.GetAxisRaw("Vertical");
    moveStep += flatRight * Input.GetAxisRaw("Horizontal");
    if (1 <= moveStep.sqrMagnitude)
    {
      moveStep.Normalize();
    }

    if (Input.GetButtonDown("Jump"))
    {
      // jump even if player pressed jump shortly after falling
      if (Time.time - coyoteTimestamp <= coyoteTime)
      {
        Jump();
      }
      else
      {
        reverseCoyoteTimestamp = Time.time;
      }
    }

    if (Input.GetButtonDown("Fire1"))
    {
      equipped.Primary();
    }
    else if (Input.GetButtonUp("Fire1"))
    {
      equipped.PrimaryRelease();
    }

    if (Input.GetButtonDown("Fire2"))
    {
      equipped.Secondary();
    }
    else if (Input.GetButtonUp("Fire2"))
    {
      equipped.SecondaryRelease();
    }

    if (Input.GetButtonDown("Fire3"))
    {
      equipped.Tertiary();
    }
    else if (Input.GetButtonUp("Fire3"))
    {
      equipped.TertiaryRelease();
    }

    if (Input.GetButtonDown("Restart"))
    {
      rb.velocity = Vector3.zero;
      transform.position = spawnPos;
      state = PlayerState.Falling;
      equipped.Unequip();
      equipped.Equip(this);
    }

    // rotate actual player body in velocity dir
    Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
    if (0.1f < flatVelocity.sqrMagnitude)
    {
      Quaternion targetRotation = Quaternion.LookRotation(flatVelocity, Vector3.up);
      bodyTransform.rotation = Quaternion.Lerp(bodyTransform.rotation, targetRotation, moveStep.magnitude * 0.5f);
    }

    animator.SetFloat("speed", rb.velocity.magnitude);
    animator.SetBool("isGrounded", PlayerState.Grounded == state);
  }

  void FixedUpdate()
  {
    if (Input.GetButton("Fire1"))
    {
      equipped.PrimaryHold();
    }

    if (Input.GetButton("Fire2"))
    {
      equipped.SecondaryHold();
    }

    if (Input.GetButton("Fire3"))
    {
      equipped.TertiaryHold();
    }

    if (PlayerState.Grounded == state)
    {
      coyoteTimestamp = Time.time;
      // jump even if player pressed jump shortly before landing
      if (Time.time - reverseCoyoteTimestamp <= reverseCoyoteTime)
      {
        Jump();
      }
    }

    if (PlayerState.Jumping == state && rb.velocity.y <= 0)
    {
      state = PlayerState.Falling;
    }

    if (PlayerState.Jumping != state)
    {
      // check for ground multiple times in a circle around player to
      // handle standing on edges reliably
      RaycastHit hit;
      uint hits = 0;
      Vector3 avgHitPoint = Vector3.zero;
      for (uint i = 0; i < numGroundRays; ++i)
      {
        Quaternion spin = Quaternion.Euler(0f, 360f * (i / (float)numGroundRays), 0f);
        if (Physics.Raycast(
          transform.position + (Vector3.up * 0.2f) + (spin * Vector3.right * groundRayRadius),
          Vector3.down, out hit,
          0.6f,
          groundMask))
        {
          ++hits;
          avgHitPoint += hit.point;
        }
      }
      if (hits > 0)
      {
        state = PlayerState.Grounded;
      }
      else
      {
        state = PlayerState.Falling;
      }
    }

    switch (state)
    {
      case PlayerState.Grounded:
        // accelerate in input direction, 
        // decelerate velocity in directions other than input,
        // keep vertical speed (necessary to reliably touch ground, 
        //   vertical speed will be reset by rigidbody on colliding with ground)
        Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        Vector3 velocityStep = moveStep * runAccel * Time.fixedDeltaTime;
        Vector3 velocityInDir = Vector3.ClampMagnitude(Vector3.Project(flatVelocity, moveStep.normalized), runSpeedMax);
        Vector3 velocityOutOfDir = flatVelocity - velocityInDir;
        rb.velocity =
          velocityInDir + velocityStep +
          Vector3.ClampMagnitude(velocityOutOfDir, Mathf.Max(0f, velocityOutOfDir.magnitude - (runDeaccel * Time.fixedDeltaTime))) +
          (Vector3.up * rb.velocity.y);
        break;
      case PlayerState.Falling:
      case PlayerState.Jumping:
        rb.velocity += moveStep * airAccel * Time.fixedDeltaTime;
        break;
    }
    rb.velocity += Vector3.down * gravity * Time.fixedDeltaTime;

    // ensure equipped physics actions are always 
    // done after player controller's own physics step
    // especially if equipment updates player's rigidbody
    equipped.PhysicsUpdate();
  }

  void Jump()
  {
    Vector3 flatVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
    rb.velocity = flatVelocity + (Vector3.up * Mathf.Max(rb.velocity.y, jumpSpeed));
    state = PlayerState.Jumping;
    coyoteTimestamp = 0f;
    reverseCoyoteTimestamp = 0f;
  }
}

