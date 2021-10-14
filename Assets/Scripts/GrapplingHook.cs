using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrapplingHook : MonoBehaviour, IEquippable
{
  Rope rope;
  PlayerController player = null;
  LayerMask groundMask;

  public void Start()
  {
    rope = transform.Find("Rope").GetComponent<Rope>();
    groundMask = LayerMask.GetMask("Ground");
  }

  public void Equip(PlayerController player)
  {
    this.player = player;
  }
  public void Unequip()
  {
    rope.Cut();
    player = null;
  }
  public void Primary()
  {
    if (!rope.isActive())
    {
      // raycast from camera to find target attach point
      // then shoot rope from actual position so that rope always starts out taut
      RaycastHit hit;
      if (Physics.Raycast(
        player.cameraTransform.position,
        player.cameraTransform.forward,
        out hit,
        rope.maxLength,
        groundMask))
      {
        rope.Shoot(
          transform.position,
          (hit.point - transform.position).normalized,
          player.rb,
          transform.position);
      }
    }
  }
  public void PrimaryHold()
  {
    rope.Shorten();
  }
  public void PrimaryRelease() { }
  public void Secondary()
  {
    rope.Cut();
  }
  public void SecondaryHold() { }
  public void SecondaryRelease() { }

  public void Tertiary() { }
  public void TertiaryHold()
  {
    rope.Extend();
  }
  public void TertiaryRelease() { }
  public void PhysicsUpdate()
  {
    rope.PhysicsUpdate();
  }
}
