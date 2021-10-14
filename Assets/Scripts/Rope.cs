using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class Rope : MonoBehaviour
{
  public int maxSegments;
  public int maxRaysPerEdge;
  public float minEdgeStep;
  public float maxLength;
  public float minLength;
  public float reelSpeed;
  public float sixOClockBoost;
  public Color restColor;
  public Color stretchedColor;
  public Color slackColor;

  bool active;
  bool speedBoostEligible;
  float length;
  float innerLength;
  Vector3[] positions;
  Vector3[] normals;
  int numSegments;
  Vector3 offsetB;
  Rigidbody rigidbodyB;
  LineRenderer lineRenderer;
  int wrapMask;
  int grappleMask;


  // this rope has the following caveats:
  //  rope is assumed to be attached to static object on non-player end
  //  rope is assumed to only be obstructed (and wraps around) by convex static objects
  //  rope can't be affected by dynamic objects other than attached player
  //  rope can't affect dynamic objects other than attached player
  //  rope is always drawn taut

  public void Start()
  {
    lineRenderer = GetComponent<LineRenderer>();

    active = false;
    positions = new Vector3[maxSegments];
    normals = new Vector3[maxSegments];
    lineRenderer.enabled = false;

    wrapMask = LayerMask.GetMask("Ground");
    grappleMask = LayerMask.GetMask("Ground");
  }

  public void Update()
  {
    if (active)
    {
      // show player that rope is stretched / relaxed by color since rope does not sag
      Color color;
      float slack = length - (Vector3.Distance(positions[numSegments - 1], positions[numSegments]) + innerLength);
      if (slack < 0f)
      {
        color = Color.Lerp(restColor, stretchedColor, -slack);
      }
      else
      {
        color = Color.Lerp(restColor, slackColor, slack);
      }
      lineRenderer.material.color = color;
    }
  }

  public void PhysicsUpdate()
  {
    if (active)
    {
      if (rigidbodyB)
      {
        // rope is split into segments each time it is 
        //  obstructed / wraps around an object
        // we only update the last segment which is connected to the player
        //  per call
        // the inner position of this segment is the point around which
        //  the player swings

        Vector3 newPosition;
        Vector3 newToOld;
        Vector3 newToInner;
        Vector3 oldToInner;
        float newToOldDistance;
        float newToInnerDistance;
        float oldToInnerDistance;
        float slack;
        RaycastHit hit;

        newPosition = rigidbodyB.position + offsetB;
        newToOld = positions[numSegments] - newPosition;
        newToOldDistance = newToOld.magnitude;
        newToOld.Normalize();

        newToInner = positions[numSegments - 1] - newPosition;
        newToInnerDistance = newToInner.magnitude;
        newToInner.Normalize();

        oldToInner = positions[numSegments - 1] - positions[numSegments];
        oldToInnerDistance = oldToInner.magnitude;
        oldToInner.Normalize();

        // innerLength is the size of all static segments of the rope,
        // i.e. all segments bar the last one connected to the player,
        // so we don't have to loop through all segments unnecessarily
        slack = length - (newToInnerDistance + innerLength);
        if (slack < 0f)
        {
          // pull player along rope proportionally with rope stretch amount,
          // cancel any velocity opposite rope origin
          // direct other velocities along rope circumference range
          rigidbodyB.velocity =
            (newToInner * -slack) +
            (newToInner * Mathf.Max(0f, Vector3.Dot(rigidbodyB.velocity, newToInner))) +
            Vector3.ProjectOnPlane(rigidbodyB.velocity, newToInner);

          // boost player speed if player swings directly underneath rope
          if (Vector3.Dot(newToInner, Vector3.down) > 0.9f)
          {
            if (speedBoostEligible && Vector3.SqrMagnitude(rigidbodyB.velocity) > 1f)
            {
              rigidbodyB.velocity += rigidbodyB.velocity.normalized * sixOClockBoost;
              speedBoostEligible = false;
            }
          }
          else
          {
            speedBoostEligible = true;
          }
        }

        if (2 <= numSegments &&
          CanUnwind(newPosition, positions[numSegments - 1], positions[numSegments - 2], normals[numSegments - 1]))
        {
          // remove last segment
          innerLength -= Vector3.Distance(positions[numSegments - 2], positions[numSegments - 1]);
          positions[numSegments - 1] = positions[numSegments];
          normals[numSegments - 1] = normals[numSegments];
          --numSegments;
        }

        // check if line of sight is obstructed between ends of the last segment
        if (Physics.Raycast(
          newPosition,
          newToInner,
          out hit,
          newToInnerDistance,
          wrapMask))
        {
          if (numSegments + 1 >= maxSegments)
          {
            Cut();
            return;
          }
          else
          {
            // split segment into two
            positions[numSegments + 1] = positions[numSegments];
            normals[numSegments + 1] = normals[numSegments];
            ++numSegments;

            positions[numSegments - 1] = FindEdge(hit, newPosition, newToOld, newToInner, out normals[numSegments - 1]);
            innerLength += Vector3.Distance(positions[numSegments - 2], positions[numSegments - 1]);
          }
        }

        positions[numSegments] = newPosition;
      }

      lineRenderer.enabled = true;
      lineRenderer.positionCount = numSegments + 1;
      lineRenderer.SetPosition(numSegments - 1, positions[numSegments - 1]);
      lineRenderer.SetPosition(numSegments, positions[numSegments]);
    }
    else
    {
      lineRenderer.enabled = false;
    }
  }

  bool CanUnwind(Vector3 outerPos, Vector3 middlePos, Vector3 innerPos, Vector3 middleNormal)
  {
    // given three sequential positions of the rope
    // if the angle facing outwards between the line from the outer position to the middle position
    // and the line from the inner position to the middle position is obtuse, and outer position is
    // in line of sight from inner position, then the two segments can be merged into one

    Vector3 innerToMiddle = (middlePos - innerPos).normalized;
    Vector3 innerToMiddleRight = Vector3.Cross(Vector3.Cross(innerToMiddle, middleNormal), innerToMiddle).normalized;
    Vector3 middleToOuter = (outerPos - middlePos).normalized;
    Vector3 innerToOuter = outerPos - innerPos;
    float innerToOuterDistance = innerToOuter.magnitude;
    innerToOuter.Normalize();

    return Vector3.Dot(innerToMiddleRight, middleToOuter) > 0f &&
      !Physics.Raycast(
      innerPos,
      innerToOuter,
      innerToOuterDistance,
      wrapMask);
  }

  Vector3 FindEdge(RaycastHit hit, Vector3 start, Vector3 oldDir, Vector3 newDir, out Vector3 normal)
  {
    // sweep from old pos to new in binary search order for approximate edge
    // until distance between iterations is insignificant or 
    // max number of iterations reached
    int rayNum = 0;
    Vector3 midDir = (oldDir + newDir) * 0.5f;
    Vector3 edge = hit.point;
    RaycastHit midHit;
    while (++rayNum < maxRaysPerEdge)
    {
      if (Physics.Raycast(
        start,
        midDir,
        out midHit,
        maxLength,
        wrapMask) &&
        midHit.collider == hit.collider)
      {
        midDir = (oldDir + midDir) * 0.5f;
        if (Vector3.Distance(edge, midHit.point) <= minEdgeStep)
        {
          edge = midHit.point;
          break;
        }
        edge = midHit.point;
      }
      else
      {
        midDir = (midDir + newDir) * 0.5f;
        edge = start + Vector3.Project(hit.point - start, midDir);
      }
    }
    // push edge normal to be diagonal, assuming that edge is a right angle
    normal = (hit.normal + oldDir).normalized;
    return edge + (hit.normal * minEdgeStep);
  }

  public void Shoot(Vector3 start, Vector3 dir, Rigidbody rigidbody, Vector3 attachPoint)
  {
    RaycastHit hit;
    if (Physics.Raycast(
      start,
      dir.normalized,
      out hit,
      maxLength,
      grappleMask))
    {
      active = true;
      speedBoostEligible = false;
      length = Mathf.Max(minLength, hit.distance);
      innerLength = 0f;

      if (rigidbody)
      {
        offsetB = attachPoint - (hit.normal * minEdgeStep) - rigidbody.position;
      }
      else
      {
        offsetB = Vector3.zero;
      }
      rigidbodyB = rigidbody;

      numSegments = 1;
      positions[0] = hit.point + (hit.normal * minEdgeStep);
      positions[1] = attachPoint - (hit.normal * minEdgeStep);
      normals[0] = hit.normal;
      normals[1] = -hit.normal;
    }
  }

  public void Cut()
  {
    active = false;
  }

  public void Extend()
  {
    length = Mathf.Min(maxLength, length + (reelSpeed * Time.fixedDeltaTime));
  }

  public void Shorten()
  {
    length = Mathf.Max(minLength, length - (reelSpeed * Time.fixedDeltaTime));
  }

  public bool isActive()
  {
    return active;
  }
}
