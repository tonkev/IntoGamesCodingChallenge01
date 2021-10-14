using UnityEngine;

public interface IEquippable
{
  void Equip(PlayerController player);
  void Unequip();
  void Primary();
  void PrimaryHold();
  void PrimaryRelease();
  void Secondary();
  void SecondaryHold();
  void SecondaryRelease();
  void Tertiary();
  void TertiaryHold();
  void TertiaryRelease();
  void PhysicsUpdate();
}