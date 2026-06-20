using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using VRBadminton.Gameplay;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private GameObject CreatePixelRacket()
        {
            GameObject root = new GameObject("Pixel Racket");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(0.8f, 0.65f, -2.7f * CourtLengthScale);

            CreateBlock("Grip", root.transform, new Vector3(0f, 0.275f, 0f),
                new Vector3(0.14f, 0.55f, 0.14f), racketDark);
            CreateBlock("Shaft", root.transform, new Vector3(0f, 0.79f, 0f),
                new Vector3(0.07f, 0.48f, 0.07f), racketRed);

            racketFace = new GameObject("Racket Face").transform;
            racketFace.SetParent(root.transform, false);
            racketFace.localPosition = new Vector3(0f, 1.35f, 0f);

            CreateBlock("Frame Top", racketFace, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketRed);
            CreateBlock("Frame Bottom", racketFace, new Vector3(0f, -0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketRed);
            CreateBlock("Frame Left", racketFace, new Vector3(-0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketRed);
            CreateBlock("Frame Right", racketFace, new Vector3(0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketRed);

            for (int i = -2; i <= 2; i++)
            {
                CreateBlock($"Vertical String {i + 3}", racketFace, new Vector3(i * 0.095f, 0f, 0.015f),
                    new Vector3(0.018f, 0.68f, 0.018f), racketString);
            }

            for (int i = -3; i <= 3; i++)
            {
                CreateBlock($"Horizontal String {i + 4}", racketFace, new Vector3(0f, i * 0.09f, 0.015f),
                    new Vector3(0.5f, 0.018f, 0.018f), racketString);
            }

            return root;
        }

        private GameObject CreatePlayerMarker()
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Cube);
            player.name = "Player Marker";
            player.transform.SetParent(transform, false);
            player.transform.localScale = new Vector3(0.55f, 1.1f, 0.4f);
            player.GetComponent<MeshRenderer>().sharedMaterial = racketDark;
            Destroy(player.GetComponent<Collider>());
            return player;
        }

        private GameObject CreateOpponentPlayer()
        {
            GameObject root = new GameObject("Opponent Player");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(
                -1.25f,
                0.55f,
                2.45f * CourtLengthScale);

            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = "Opponent Body";
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localScale = new Vector3(0.58f, 1.1f, 0.4f);
            bodyObject.GetComponent<MeshRenderer>().sharedMaterial = racketBlue;
            Destroy(bodyObject.GetComponent<Collider>());
            opponentBody = bodyObject.transform;
            return root;
        }

        private GameObject CreateOpponentRacket(Transform playerRoot)
        {
            GameObject root = new GameObject("Opponent Pixel Racket");
            root.transform.SetParent(playerRoot, false);
            root.transform.localPosition = GetOpponentReadyRacketPosition();
            root.transform.localRotation = GetOpponentReadyRacketRotation();

            CreateBlock("Grip", root.transform, new Vector3(0f, 0.275f, 0f),
                new Vector3(0.14f, 0.55f, 0.14f), racketDark);
            CreateBlock("Shaft", root.transform, new Vector3(0f, 0.79f, 0f),
                new Vector3(0.07f, 0.48f, 0.07f), racketBlue);

            opponentRacketFace = new GameObject("Opponent Racket Face").transform;
            opponentRacketFace.SetParent(root.transform, false);
            opponentRacketFace.localPosition = new Vector3(0f, 1.35f, 0f);

            CreateBlock("Frame Top", opponentRacketFace, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketBlue);
            CreateBlock("Frame Bottom", opponentRacketFace, new Vector3(0f, -0.38f, 0f),
                new Vector3(0.58f, 0.09f, 0.09f), racketBlue);
            CreateBlock("Frame Left", opponentRacketFace, new Vector3(-0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketBlue);
            CreateBlock("Frame Right", opponentRacketFace, new Vector3(0.29f, 0f, 0f),
                new Vector3(0.09f, 0.76f, 0.09f), racketBlue);

            for (int i = -2; i <= 2; i++)
            {
                CreateBlock($"Vertical String {i + 3}", opponentRacketFace, new Vector3(i * 0.095f, 0f, 0.015f),
                    new Vector3(0.018f, 0.68f, 0.018f), racketString);
            }

            for (int i = -3; i <= 3; i++)
            {
                CreateBlock($"Horizontal String {i + 4}", opponentRacketFace, new Vector3(0f, i * 0.09f, 0.015f),
                    new Vector3(0.5f, 0.018f, 0.018f), racketString);
            }

            return root;
        }

        private GameObject CreateShuttlecock()
        {
            GameObject root = new GameObject("Shuttlecock");
            root.transform.SetParent(transform, false);

            CreateBlock("Cork", root.transform, new Vector3(0f, 0f, 0.13f),
                new Vector3(0.14f, 0.14f, 0.12f), shuttleCork);
            CreateBlock("White Band", root.transform, new Vector3(0f, 0f, 0.045f),
                new Vector3(0.16f, 0.16f, 0.055f), shuttleWhite);
            CreateBlock("Feather Core", root.transform, new Vector3(0f, 0f, -0.055f),
                new Vector3(0.1f, 0.1f, 0.16f), shuttleWhite);
            CreateBlock("Feather Top", root.transform, new Vector3(0f, 0.09f, -0.16f),
                new Vector3(0.08f, 0.13f, 0.18f), shuttleWhite);
            CreateBlock("Feather Bottom", root.transform, new Vector3(0f, -0.09f, -0.16f),
                new Vector3(0.08f, 0.13f, 0.18f), shuttleWhite);
            CreateBlock("Feather Left", root.transform, new Vector3(-0.09f, 0f, -0.16f),
                new Vector3(0.13f, 0.08f, 0.18f), shuttleWhite);
            CreateBlock("Feather Right", root.transform, new Vector3(0.09f, 0f, -0.16f),
                new Vector3(0.13f, 0.08f, 0.18f), shuttleWhite);

            GameObject trailAnchor = new GameObject("Trail");
            trailAnchor.transform.SetParent(root.transform, false);
            trailAnchor.transform.localPosition = new Vector3(0f, 0f, -0.25f);
            shuttleTrail = trailAnchor.AddComponent<TrailRenderer>();
            shuttleTrail.time = 0.42f;
            shuttleTrail.minVertexDistance = 0.035f;
            shuttleTrail.startWidth = 0.085f;
            shuttleTrail.endWidth = 0f;
            shuttleTrail.material = trailMaterial;
            shuttleTrail.startColor = new Color(1f, 0.95f, 0.55f, 0.75f);
            shuttleTrail.endColor = new Color(1f, 1f, 1f, 0f);
            shuttleTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shuttleTrail.receiveShadows = false;
            root.transform.localScale = Vector3.one * 0.9f;
            return root;
        }

        private GameObject CreateLandingMarker()
        {
            GameObject marker = new GameObject("Landing Marker");
            marker.transform.SetParent(transform, false);

            MeshFilter meshFilter = marker.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = marker.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = CreateRingMesh(0.42f, 0.06f, 48);
            meshRenderer.sharedMaterial = markerYellow;

            GameObject center = new GameObject("Landing Center");
            center.transform.SetParent(marker.transform, false);
            center.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            MeshFilter centerFilter = center.AddComponent<MeshFilter>();
            MeshRenderer centerRenderer = center.AddComponent<MeshRenderer>();
            centerFilter.sharedMesh = CreateDiscMesh(0.1f, 24);
            centerRenderer.sharedMaterial = markerYellow;
            return marker;
        }

        private GameObject CreatePlayerPositionMarker()
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Player Position Marker";
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = new Vector3(0.55f, 0.025f, 0.55f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = playerPositionMaterial;
            Destroy(marker.GetComponent<Collider>());
            return marker;
        }

        private GameObject CreateTrajectoryGuide()
        {
            GameObject root = new GameObject("Ground Trajectory Guide");
            root.transform.SetParent(transform, false);

            const int dashCount = 18;
            for (int i = 0; i < dashCount; i++)
            {
                GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                dash.name = $"Trajectory Dash {i + 1:00}";
                dash.transform.SetParent(root.transform, false);
                dash.transform.localScale = new Vector3(0.08f, 0.018f, 0.22f);
                dash.GetComponent<MeshRenderer>().sharedMaterial = trajectoryMaterial;
                Destroy(dash.GetComponent<Collider>());
            }

            GameObject apex = GameObject.CreatePrimitive(PrimitiveType.Cube);
            apex.name = "Apex Projection";
            apex.transform.SetParent(root.transform, false);
            apex.transform.localScale = new Vector3(0.28f, 0.025f, 0.28f);
            apex.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            apex.GetComponent<MeshRenderer>().sharedMaterial = markerYellow;
            Destroy(apex.GetComponent<Collider>());
            apexProjection = apex.transform;

            return root;
        }

        private GameObject CreateRacketCenterGuide()
        {
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "Racket Center Ground Guide";
            guide.transform.SetParent(transform, false);
            guide.transform.localScale = new Vector3(0.82f, 0.018f, 0.055f);
            guide.GetComponent<MeshRenderer>().sharedMaterial = playerPositionMaterial;
            Destroy(guide.GetComponent<Collider>());
            return guide;
        }

        private void ShowLandingPrediction(
            Vector3 start,
            Vector3 target,
            float apexT = 0.5f,
            bool showApex = false)
        {
            landingMarker.position = new Vector3(target.x, 0.025f, target.z);
            landingMarker.gameObject.SetActive(true);

            Vector3 groundStart = new Vector3(start.x, 0.02f, start.z);
            Vector3 groundTarget = new Vector3(target.x, 0.02f, target.z);
            Vector3 direction = groundTarget - groundStart;
            float distance = direction.magnitude;
            if (distance < 0.01f)
            {
                trajectoryGuide.gameObject.SetActive(false);
                return;
            }

            trajectoryGuide.gameObject.SetActive(true);
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            int dashCount = trajectoryGuide.childCount - 1;
            for (int i = 0; i < dashCount; i++)
            {
                float t = (i + 0.5f) / dashCount;
                Transform dash = trajectoryGuide.GetChild(i);
                dash.position = Vector3.Lerp(groundStart, groundTarget, t);
                dash.rotation = rotation;
                dash.localScale = new Vector3(
                    0.08f,
                    0.018f,
                    Mathf.Min(0.28f, distance / (dashCount * 1.65f)));
            }

            apexProjection.gameObject.SetActive(true);
            apexProjection.position = groundStart + Vector3.up * 0.014f;
            apexProjection.rotation = Quaternion.Euler(0f, 45f, 0f);
        }

        private void HideLandingPrediction()
        {
            if (landingMarker != null)
            {
                landingMarker.gameObject.SetActive(false);
            }

            if (trajectoryGuide != null)
            {
                trajectoryGuide.gameObject.SetActive(false);
            }

            if (apexProjection != null)
            {
                apexProjection.gameObject.SetActive(false);
            }
        }

        private static void CreateBlock(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(block.GetComponent<Collider>());
        }

        private static Mesh CreateRingMesh(float radius, float thickness, int segments)
        {
            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];
            float innerRadius = radius - thickness;

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = radial * innerRadius;
                vertices[i * 2 + 1] = radial * radius;
                int next = (i + 1) % segments;
                int triangle = i * 6;
                triangles[triangle] = i * 2;
                triangles[triangle + 1] = next * 2 + 1;
                triangles[triangle + 2] = i * 2 + 1;
                triangles[triangle + 3] = i * 2;
                triangles[triangle + 4] = next * 2;
                triangles[triangle + 5] = next * 2 + 1;
            }

            Mesh mesh = new Mesh { name = "Landing Marker Ring" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateDiscMesh(float radius, int segments)
        {
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                int next = (i + 1) % segments;
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = next + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            Mesh mesh = new Mesh { name = "Landing Marker Center" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void CreateMaterials()
        {
            Shader shader = Shader.Find("Standard");
            shuttleWhite = CreateRuntimeMaterial(shader, "Shuttle White", new Color(0.96f, 0.97f, 0.94f));
            shuttleCork = CreateRuntimeMaterial(shader, "Shuttle Cork", new Color(0.93f, 0.88f, 0.72f));
            markerYellow = CreateRuntimeMaterial(shader, "Landing Marker Yellow", new Color(1f, 0.78f, 0.04f));
            playerPositionMaterial = CreateRuntimeMaterial(
                shader,
                "Player Position Cyan",
                new Color(0.05f, 0.85f, 0.9f));
            trajectoryMaterial = CreateRuntimeMaterial(
                shader,
                "Trajectory Dark",
                new Color(0.22f, 0.22f, 0.18f));
            racketRed = CreateRuntimeMaterial(shader, "Racket Red", new Color(0.82f, 0.08f, 0.1f));
            racketBlue = CreateRuntimeMaterial(shader, "Opponent Racket Blue", new Color(0.08f, 0.32f, 0.85f));
            racketDark = CreateRuntimeMaterial(shader, "Racket Grip", new Color(0.05f, 0.06f, 0.08f));
            racketString = CreateRuntimeMaterial(shader, "Racket Strings", new Color(0.92f, 0.94f, 0.9f));

            Shader trailShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (trailShader == null)
            {
                trailShader = Shader.Find("Sprites/Default");
            }

            trailMaterial = CreateRuntimeMaterial(trailShader, "Shuttle Trail", Color.white);
            trailMaterial.renderQueue = 3000;
        }

        private static Material CreateRuntimeMaterial(Shader shader, string name, Color color)
        {
            return new Material(shader)
            {
                name = name,
                color = color
            };
        }
    }
}
