using UnityEngine;
using HoloLensApp.Sandbox.Csg;

namespace HoloLensApp.Sandbox
{
    public static class FormInteractionSolver
    {
        public static void ApplyOperation(WongOperation op, GameObject shapeA, GameObject shapeB)
        {
            if (shapeA == null || shapeB == null) return;

            switch (op)
            {
                case WongOperation.Detachment:
                    ApplyDetachment(shapeA, shapeB);
                    break;
                case WongOperation.Touching:
                    ApplyTouching(shapeA, shapeB);
                    break;
                case WongOperation.Overlapping:
                    ApplyOverlapping(shapeA, shapeB);
                    break;
                case WongOperation.Penetration:
                    ApplyPenetration(shapeA, shapeB);
                    break;
                case WongOperation.Coinciding:
                    ApplyCoinciding(shapeA, shapeB);
                    break;
                case WongOperation.Union:
                    ApplyCSG(CSG.BooleanOp.Union, shapeA, shapeB);
                    break;
                case WongOperation.Subtraction:
                    // Shape B acts as negative volume to carve Shape A
                    ApplyCSG(CSG.BooleanOp.Subtraction, shapeA, shapeB);
                    break;
                case WongOperation.Intersection:
                    ApplyCSG(CSG.BooleanOp.Intersection, shapeA, shapeB);
                    break;
            }
        }

        private static void ApplyDetachment(GameObject a, GameObject b)
        {
            // Move apart based on combined bounds size so they do not touch
            Bounds boundsA = GetBounds(a);
            Bounds boundsB = GetBounds(b);
            float minDistance = (boundsA.extents.magnitude + boundsB.extents.magnitude) * 1.5f;
            
            Vector3 direction = (b.transform.position - a.transform.position).normalized;
            if (direction == Vector3.zero) direction = Vector3.right;
            
            b.transform.position = a.transform.position + direction * minDistance;
        }

        private static void ApplyTouching(GameObject a, GameObject b)
        {
            // Snap colliders exactly
            Bounds boundsA = GetBounds(a);
            Bounds boundsB = GetBounds(b);
            float touchDistance = (boundsA.extents.x + boundsB.extents.x); // Simplistic touching on X axis for now
            
            Vector3 direction = (b.transform.position - a.transform.position).normalized;
            if (direction == Vector3.zero) direction = Vector3.right;

            // Better touching logic: snap to the closest bounds face.
            b.transform.position = a.transform.position + direction * touchDistance;
        }

        private static void ApplyOverlapping(GameObject a, GameObject b)
        {
            // Position closely with slight offset
            Bounds boundsA = GetBounds(a);
            b.transform.position = a.transform.position + new Vector3(boundsA.extents.x * 0.5f, boundsA.extents.y * 0.5f, 0);
        }

        private static void ApplyPenetration(GameObject a, GameObject b)
        {
            // Intersect directly
            b.transform.position = a.transform.position + new Vector3(GetBounds(a).extents.x * 0.2f, 0, 0);
        }

        private static void ApplyCoinciding(GameObject a, GameObject b)
        {
            // Identical transform
            b.transform.position = a.transform.position;
            b.transform.rotation = a.transform.rotation;
            b.transform.localScale = a.transform.localScale;
        }

        private static void ApplyCSG(CSG.BooleanOp csgOp, GameObject lhs, GameObject rhs)
        {
            try
            {
                // Perform CSG operation using ProBuilder's CSG logic
                Model resultModel = CSG.Perform(csgOp, lhs, rhs);
                
                if (resultModel != null)
                {
                    Mesh resultMesh = (Mesh)resultModel;
                    resultMesh.name = $"CSG_Result_{csgOp}";

                    // Assign to LHS
                    MeshFilter lhsFilter = lhs.GetComponent<MeshFilter>();
                    if (lhsFilter != null) lhsFilter.sharedMesh = resultMesh;

                    MeshCollider lhsCollider = lhs.GetComponent<MeshCollider>();
                    if (lhsCollider != null)
                    {
                        lhsCollider.sharedMesh = resultMesh;
                    }
                    else
                    {
                        // Remove existing colliders and add mesh collider
                        var oldColliders = lhs.GetComponents<Collider>();
                        foreach(var col in oldColliders) Object.Destroy(col);
                        lhsCollider = lhs.AddComponent<MeshCollider>();
                        lhsCollider.sharedMesh = resultMesh;
                        lhsCollider.convex = true; // Ensure it works with Rigidbody
                    }

                    // Update materials if needed (using LHS materials for simplicity)
                    MeshRenderer lhsRenderer = lhs.GetComponent<MeshRenderer>();
                    if (lhsRenderer != null && resultModel.materials != null && resultModel.materials.Count > 0)
                    {
                        lhsRenderer.sharedMaterials = resultModel.materials.ToArray();
                    }

                    SandboxObject lhsSbObj = lhs.GetComponent<SandboxObject>();
                    if (lhsSbObj != null) lhsSbObj.UpdateOriginalMaterials();

                    // Re-apply physics & interaction so the baked shape can be grabbed
                    Rigidbody rb = lhs.GetComponent<Rigidbody>();
                    if (rb == null) rb = lhs.AddComponent<Rigidbody>();
                    rb.isKinematic = true; // Solidified and persistent, but can be grabbed

                    var grab = lhs.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    if (grab == null) grab = lhs.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
                    grab.throwOnDetach = true;
                    grab.throwVelocityScale = 2.0f;

                    // Destroy RHS since it has been consumed by the operation (except for Union where we might keep both, but standard CSG merges them)
                    Object.Destroy(rhs);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[FormInteractionSolver] CSG Operation {csgOp} failed: {e.Message}");
            }
        }

        private static Bounds GetBounds(GameObject obj)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) return r.bounds;
            
            Collider c = obj.GetComponent<Collider>();
            if (c != null) return c.bounds;
            
            return new Bounds(obj.transform.position, Vector3.one);
        }
    }
}
