using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CameraController : MonoBehaviour
{
  public float mix;
  public Transform target;

  LayerMask groundMask;

  void Start()
  {
    groundMask = LayerMask.GetMask("Ground");
  }

  void FixedUpdate()
  {
    Vector3 targetPosition = target.position;
    Vector3 parentToTarget = targetPosition - target.parent.position;
    float parentToTargetDistance = parentToTarget.magnitude;
    parentToTarget.Normalize();

    RaycastHit hit;
    if (Physics.Raycast(
      target.parent.position,
      parentToTarget,
      out hit,
      parentToTargetDistance,
      groundMask))
    {
      targetPosition = hit.point - (parentToTarget * 0.1f);
    }

    transform.position = Vector3.Lerp(transform.position, targetPosition, mix);
    transform.rotation = Quaternion.Lerp(transform.rotation, target.rotation, mix);
  }
}
