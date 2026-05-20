using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloLensApp.Interaction.Snapping;
using HoloLensApp.Interaction.CSG;
using HoloLensApp.Interaction.Math;
// Note: Depending on XRI version, this might be UnityEngine.XR.Interaction.Toolkit
using UnityEngine.XR.Interaction.Toolkit.Interactables; 

namespace HoloLensApp.Interaction.Prompt
{
    /// <summary>
    /// Processes simple text commands from the user/AI menu to manipulate the scene.
    /// Example: "1 küp oluştur ve 10'a böl"
    /// </summary>
    public class PromptCommandController : MonoBehaviour
    {
        [Header("Materials for Generated Objects")]
        public Material DefaultCubeMaterial;
        public Material RedTestCubeMaterial;

        /// <summary>
        /// Reads the text input and executes the corresponding scene action.
        /// </summary>
        public void ProcessCommand(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return;

            string lowerPrompt = prompt.ToLower();

            // Check for the specific "küp oluştur ve böl" command scenario
            if (lowerPrompt.Contains("küp") && (lowerPrompt.Contains("oluştur") || lowerPrompt.Contains("böl") || lowerPrompt.Contains("ayır")))
            {
                ExecuteVoxelizationScenario();
            }
            else if (lowerPrompt.Contains("kes") || lowerPrompt.Contains("çıkar") || lowerPrompt.Contains("birleştir"))
            {
                ExecuteCSGScenario(lowerPrompt);
            }
            else
            {
                Debug.Log($"[PromptCommand] Unrecognized command: {prompt}");
            }
        }

        /// <summary>
        /// Finds two overlapping cubes in the scene and performs a CSG operation.
        /// </summary>
        private void ExecuteCSGScenario(string lowerPrompt)
        {
            Debug.Log("[PromptCommand] Executing CSG physical mesh generation...");

            // Find the Red Test Cube (Main Object)
            GameObject testCube = GameObject.Find("Red_Test_Cube");
            if (testCube == null)
            {
                Debug.LogError("Red_Test_Cube not found! Lütfen önce küpleri oluştur komutunu verin.");
                return;
            }

            // Find any overlapping SubCube
            Collider[] colliders = Physics.OverlapBox(testCube.transform.position, testCube.transform.localScale / 2f);
            GameObject overlappingSubCube = null;

            foreach (var col in colliders)
            {
                if (col.gameObject != testCube && col.name == "SubCube")
                {
                    overlappingSubCube = col.gameObject;
                    break; // Just grab the first one we overlap with
                }
            }

            if (overlappingSubCube != null)
            {
                CSGOperationType op = CSGOperationType.Subtraction; // Default
                if (lowerPrompt.Contains("kes")) op = CSGOperationType.Intersection;
                if (lowerPrompt.Contains("birleştir")) op = CSGOperationType.Union;

                // Here we call the magic line!
                if (ShapeInteractionManager.Instance != null)
                    ShapeInteractionManager.Instance.RequestCSG(testCube, overlappingSubCube, op);
                else
                    CSGFormManager.Instance?.ProcessCSGOperation(testCube, overlappingSubCube, op);
                Debug.Log($"[PromptCommand] {op} işlemi başlatıldı!");
            }
            else
            {
                Debug.LogWarning("Test küpü hiçbir alt-küp ile temas etmiyor! Lütfen küpleri iç içe sokun.");
            }
        }

        /// <summary>
        /// Instantiates 1000 small cubes in a 10x10x10 matrix and a Red Test Cube.
        /// </summary>
        private void ExecuteVoxelizationScenario()
        {
            Debug.Log("[PromptCommand] Executing voxelization scenario...");

            float mainCubeSize = 1.0f;
            int subdivisions = 10;
            float subCubeSize = mainCubeSize / subdivisions; // 0.1f

            Vector3 startPos = new Vector3(0, 1f, 0); // Center of the main conceptual cube
            Vector3 offset = new Vector3(-mainCubeSize/2f + subCubeSize/2f, -mainCubeSize/2f + subCubeSize/2f, -mainCubeSize/2f + subCubeSize/2f);

            // 1. Create the 1000 Sub-Cubes
            for (int x = 0; x < subdivisions; x++)
            {
                for (int y = 0; y < subdivisions; y++)
                {
                    for (int z = 0; z < subdivisions; z++)
                    {
                        Vector3 pos = startPos + offset + new Vector3(x * subCubeSize, y * subCubeSize, z * subCubeSize);
                        CreateSubCube(pos, subCubeSize);
                    }
                }
            }

            // 2. Create the Red Test Cube
            CreateTestCube(startPos + new Vector3(1.5f, 0, 0)); // Place it a bit to the right
        }

        private void CreateSubCube(Vector3 position, float size)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SubCube";
            cube.transform.position = position;
            cube.transform.localScale = new Vector3(size, size, size);

            if (DefaultCubeMaterial != null)
            {
                cube.GetComponent<Renderer>().material = DefaultCubeMaterial;
            }

            // Add Rigidbody (Required by XR Grab)
            Rigidbody rb = cube.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;

            // Add XR Grab Interactable
            cube.AddComponent<XRGrabInteractable>();

            // Add our Custom Snapping Logic
            cube.AddComponent<GridSnapper>();
            cube.AddComponent<FormInteractable>();
        }

        private void CreateTestCube(Vector3 position)
        {
            GameObject testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testCube.name = "Red_Test_Cube";
            testCube.transform.position = position;
            testCube.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

            if (RedTestCubeMaterial != null)
            {
                testCube.GetComponent<Renderer>().material = RedTestCubeMaterial;
            }
            else
            {
                testCube.GetComponent<Renderer>().material.color = Color.red; // Fallback
            }

            // Test cube shouldn't fall or snap, just be movable.
            Rigidbody rb = testCube.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true; // Make it kinematic so we can move it through others to test volume

            // Add XR Grab Interactable so user can move it
            testCube.AddComponent<XRGrabInteractable>();

            // Add the Volume Debugger script
            testCube.AddComponent<VolumeDebugger>();
            testCube.AddComponent<FormInteractable>();
        }
    }
}
