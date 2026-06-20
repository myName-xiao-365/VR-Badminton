using UnityEngine;
using UnityEngine.Rendering;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private enum SceneTheme
        {
            Meadow,
            Desert,
            Mountains,
            Beach
        }

        private readonly GameObject[] sceneThemeRoots = new GameObject[4];
        private SceneTheme selectedSceneTheme = SceneTheme.Meadow;
        private Light sceneDirectionalLight;
        private float defaultDirectionalIntensity = 1f;
        private Color defaultDirectionalColor = Color.white;

        private void CreateMinecraftBackground()
        {
            Shader shader = Shader.Find("Standard");
            sceneThemeRoots[(int)SceneTheme.Meadow] = CreateMeadowTheme(shader);
            sceneThemeRoots[(int)SceneTheme.Desert] = CreateDesertTheme(shader);
            sceneThemeRoots[(int)SceneTheme.Mountains] = CreateMountainsTheme(shader);
            sceneThemeRoots[(int)SceneTheme.Beach] = CreateBeachTheme(shader);
            ApplySceneTheme();
        }

        private GameObject CreateThemeRoot(string name)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(transform, false);
            return root;
        }

        private GameObject CreateMeadowTheme(Shader shader)
        {
            GameObject root = CreateThemeRoot("MC Scene - Meadow");
            Material grass = CreateBackdropMaterial(shader, "Meadow Grass", new Color(0.28f, 0.62f, 0.22f));
            Material dirt = CreateBackdropMaterial(shader, "Meadow Dirt", new Color(0.43f, 0.27f, 0.14f));
            Material stone = CreateBackdropMaterial(shader, "Meadow Stone", new Color(0.42f, 0.46f, 0.47f));
            Material wood = CreateBackdropMaterial(shader, "Meadow Wood", new Color(0.36f, 0.22f, 0.10f));
            Material leaves = CreateBackdropMaterial(shader, "Meadow Leaves", new Color(0.14f, 0.46f, 0.18f));
            Material cloud = CreateBackdropMaterial(shader, "Meadow Cloud", new Color(0.94f, 0.97f, 1f));
            Material sun = CreateBackdropMaterial(shader, "Meadow Sun", new Color(1f, 0.72f, 0.12f), true);

            CreateGround(root.transform, grass);
            CreateTerraces(root.transform, dirt, grass, stone, 0.8f);
            CreateBlockTree(root.transform, new Vector3(-8.3f, 0f, 4.8f), 1.05f, wood, leaves);
            CreateBlockTree(root.transform, new Vector3(8.5f, 0f, 6.4f), 1.2f, wood, leaves);
            CreateBlockTree(root.transform, new Vector3(-10.8f, 0f, 11.5f), 1.15f, wood, leaves);
            CreateBlockTree(root.transform, new Vector3(10.5f, 0f, 13f), 0.95f, wood, leaves);
            CreateCloud(root.transform, new Vector3(-6.8f, 7.2f, 16.5f), 1.15f, cloud);
            CreateCloud(root.transform, new Vector3(6.4f, 8.2f, 19f), 0.9f, cloud);
            CreateBackdropBlock("Pixel Sun", root.transform, new Vector3(-8.5f, 8.4f, 21f),
                new Vector3(2.2f, 2.2f, 0.35f), sun, false);
            return root;
        }

        private GameObject CreateDesertTheme(Shader shader)
        {
            GameObject root = CreateThemeRoot("MC Scene - Desert");
            Material sand = CreateBackdropMaterial(shader, "Desert Sand", new Color(0.76f, 0.56f, 0.28f));
            Material sandstone = CreateBackdropMaterial(shader, "Sandstone", new Color(0.66f, 0.37f, 0.18f));
            Material cactus = CreateBackdropMaterial(shader, "Cactus", new Color(0.15f, 0.42f, 0.16f));
            Material sun = CreateBackdropMaterial(shader, "Desert Sun", new Color(1f, 0.57f, 0.08f), true);

            CreateGround(root.transform, sand);
            CreateTerraces(root.transform, sandstone, sand, sandstone, 0.55f);
            CreateCactus(root.transform, new Vector3(-8.2f, 0f, 4f), 1.1f, cactus);
            CreateCactus(root.transform, new Vector3(8.8f, 0f, 8f), 1.35f, cactus);
            CreateCactus(root.transform, new Vector3(-10.5f, 0f, 12f), 0.9f, cactus);
            CreateBackdropBlock("Desert Sun", root.transform, new Vector3(7.5f, 7.7f, 20f),
                new Vector3(2.7f, 2.7f, 0.35f), sun, false);
            return root;
        }

        private GameObject CreateMountainsTheme(Shader shader)
        {
            GameObject root = CreateThemeRoot("MC Scene - Mountains");
            Material rock = CreateBackdropMaterial(shader, "Mountain Rock", new Color(0.34f, 0.39f, 0.42f));
            Material darkRock = CreateBackdropMaterial(shader, "Mountain Shadow Rock", new Color(0.22f, 0.27f, 0.3f));
            Material snow = CreateBackdropMaterial(shader, "Mountain Snow", new Color(0.9f, 0.94f, 0.96f));
            Material cloud = CreateBackdropMaterial(shader, "Mountain Cloud", new Color(0.92f, 0.96f, 1f));
            Material coalOre = CreateBackdropMaterial(shader, "Exposed Coal Ore", new Color(0.055f, 0.06f, 0.065f));
            Material ironOre = CreateBackdropMaterial(shader, "Exposed Iron Ore", new Color(0.56f, 0.34f, 0.22f));

            CreateGround(root.transform, rock);
            CreateMountain(root.transform, new Vector3(-9f, 0f, 14.8f), 5.4f, 7.2f, darkRock, snow);
            CreateMountain(root.transform, new Vector3(-3.8f, 0f, 16.8f), 6.2f, 9.1f, rock, snow);
            CreateMountain(root.transform, new Vector3(2.4f, 0f, 17.5f), 7.2f, 10.2f, darkRock, snow);
            CreateMountain(root.transform, new Vector3(8.7f, 0f, 15.2f), 5.8f, 7.8f, rock, snow);
            CreateMountain(root.transform, new Vector3(-11f, 0f, 8f), 3.8f, 4.7f, rock, snow);
            CreateMountain(root.transform, new Vector3(11f, 0f, 9f), 4.1f, 5.2f, darkRock, snow);
            CreateOreVein(root.transform, new Vector3(-8.8f, 1.65f, 12.82f), coalOre);
            CreateOreVein(root.transform, new Vector3(-3.5f, 2.55f, 14.53f), ironOre);
            CreateOreVein(root.transform, new Vector3(2.9f, 3.25f, 14.86f), coalOre);
            CreateOreVein(root.transform, new Vector3(8.4f, 2.1f, 13.08f), ironOre);
            CreateOreVein(root.transform, new Vector3(-10.8f, 1.25f, 6.58f), ironOre);
            CreateCloud(root.transform, new Vector3(-6.5f, 6.5f, 13f), 0.9f, cloud);
            CreateCloud(root.transform, new Vector3(7f, 7.5f, 19f), 1.05f, cloud);
            return root;
        }

        private GameObject CreateBeachTheme(Shader shader)
        {
            GameObject root = CreateThemeRoot("MC Scene - Beach");
            Material sand = CreateBackdropMaterial(shader, "Beach Sand", new Color(0.82f, 0.68f, 0.4f));
            Material water = CreateBackdropMaterial(shader, "Ocean Water", new Color(0.05f, 0.5f, 0.72f), true);
            Material wood = CreateBackdropMaterial(shader, "Palm Wood", new Color(0.42f, 0.25f, 0.1f));
            Material leaves = CreateBackdropMaterial(shader, "Palm Leaves", new Color(0.08f, 0.46f, 0.2f));
            Material cloud = CreateBackdropMaterial(shader, "Beach Cloud", new Color(0.96f, 0.98f, 1f));
            Material sun = CreateBackdropMaterial(shader, "Beach Sun", new Color(1f, 0.75f, 0.18f), true);

            CreateGround(root.transform, sand);
            float[] shoreDepths = { 10.8f, 9.5f, 10.2f, 8.9f, 9.7f, 11.1f, 10.3f };
            const float oceanFarEdge = 24f;
            for (int i = 0; i < shoreDepths.Length; i++)
            {
                float shore = shoreDepths[i];
                float depth = oceanFarEdge - shore;
                float x = -15f + i * 5f;
                CreateBackdropBlock("Ocean Bay", root.transform,
                    new Vector3(x, -0.02f, shore + depth * 0.5f),
                    new Vector3(5.15f, 0.2f, depth), water, false);
            }
            CreatePalmTree(root.transform, new Vector3(-8.3f, 0f, 3.5f), 1.15f, wood, leaves);
            CreatePalmTree(root.transform, new Vector3(8.5f, 0f, 5.8f), 1.3f, wood, leaves);
            CreatePalmTree(root.transform, new Vector3(-10.5f, 0f, 9f), 0.9f, wood, leaves);
            CreateBackdropBlock("Beach Sun", root.transform, new Vector3(-7.5f, 8.2f, 22f),
                new Vector3(2.5f, 2.5f, 0.35f), sun, false);
            CreateCloud(root.transform, new Vector3(5.8f, 7.2f, 20f), 0.95f, cloud);
            return root;
        }

        private void SelectSceneTheme(int themeIndex)
        {
            selectedSceneTheme = (SceneTheme)Mathf.Clamp(themeIndex, 0, sceneThemeRoots.Length - 1);
            ApplySceneTheme();
        }

        private void ApplySceneTheme()
        {
            for (int i = 0; i < sceneThemeRoots.Length; i++)
            {
                if (sceneThemeRoots[i] != null)
                {
                    sceneThemeRoots[i].SetActive(i == (int)selectedSceneTheme);
                }
            }

            ResolveDirectionalLight();
            Color sky;
            Color ambient;
            float directionalIntensity;
            Color directionalColor;
            switch (selectedSceneTheme)
            {
                case SceneTheme.Desert:
                    sky = new Color(0.88f, 0.58f, 0.3f);
                    ambient = new Color(0.74f, 0.56f, 0.38f);
                    directionalIntensity = defaultDirectionalIntensity * 1.12f;
                    directionalColor = new Color(1f, 0.84f, 0.62f);
                    break;
                case SceneTheme.Mountains:
                    sky = new Color(0.38f, 0.62f, 0.82f);
                    ambient = new Color(0.55f, 0.64f, 0.7f);
                    directionalIntensity = defaultDirectionalIntensity * 0.92f;
                    directionalColor = new Color(0.82f, 0.9f, 1f);
                    break;
                case SceneTheme.Beach:
                    sky = new Color(0.32f, 0.7f, 0.94f);
                    ambient = new Color(0.72f, 0.7f, 0.56f);
                    directionalIntensity = defaultDirectionalIntensity * 1.12f;
                    directionalColor = new Color(1f, 0.9f, 0.72f);
                    break;
                default:
                    sky = new Color(0.38f, 0.67f, 0.88f);
                    ambient = new Color(0.63f, 0.69f, 0.72f);
                    directionalIntensity = defaultDirectionalIntensity;
                    directionalColor = defaultDirectionalColor;
                    break;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            if (sceneDirectionalLight != null)
            {
                sceneDirectionalLight.intensity = directionalIntensity;
                sceneDirectionalLight.color = directionalColor;
            }
            Camera camera = gameplayCameraOverride != null ? gameplayCameraOverride : Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = sky;
            }
        }

        private void ResolveDirectionalLight()
        {
            if (sceneDirectionalLight != null)
            {
                return;
            }

            Light[] lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Directional)
                {
                    continue;
                }

                sceneDirectionalLight = lights[i];
                defaultDirectionalIntensity = sceneDirectionalLight.intensity;
                defaultDirectionalColor = sceneDirectionalLight.color;
                break;
            }
        }

        private static string GetSceneThemeLabel(int themeIndex)
        {
            string[] labels = { "Meadow", "Desert", "Mountains", "Beach" };
            return labels[Mathf.Clamp(themeIndex, 0, labels.Length - 1)];
        }

        private static void CreateMountain(
            Transform root,
            Vector3 position,
            float width,
            float height,
            Material rock,
            Material snow)
        {
            CreateBackdropBlock("Mountain Base", root,
                position + Vector3.up * height * 0.22f,
                new Vector3(width, height * 0.44f, width * 0.72f), rock);
            CreateBackdropBlock("Mountain Middle", root,
                position + Vector3.up * height * 0.55f,
                new Vector3(width * 0.7f, height * 0.38f, width * 0.56f), rock);
            CreateBackdropBlock("Mountain Peak", root,
                position + Vector3.up * height * 0.82f,
                new Vector3(width * 0.4f, height * 0.28f, width * 0.38f), rock);
            CreateBackdropBlock("Snow Cap", root,
                position + Vector3.up * height * 0.97f,
                new Vector3(width * 0.44f, height * 0.12f, width * 0.42f), snow);
        }

        private static void CreateOreVein(Transform root, Vector3 position, Material ore)
        {
            CreateBackdropBlock("Exposed Ore", root, position,
                new Vector3(0.62f, 0.62f, 0.16f), ore, false);
            CreateBackdropBlock("Exposed Ore", root, position + new Vector3(0.55f, 0.38f, 0f),
                new Vector3(0.48f, 0.48f, 0.16f), ore, false);
            CreateBackdropBlock("Exposed Ore", root, position + new Vector3(-0.48f, -0.34f, 0f),
                new Vector3(0.42f, 0.42f, 0.16f), ore, false);
        }

        private static void CreatePalmTree(
            Transform root,
            Vector3 position,
            float scale,
            Material wood,
            Material leaves)
        {
            for (int i = 0; i < 4; i++)
            {
                CreateBackdropBlock("Palm Trunk", root,
                    position + new Vector3(i * 0.12f, 0.45f + i * 0.82f, 0f) * scale,
                    new Vector3(0.42f, 0.9f, 0.42f) * scale, wood);
            }

            Vector3 crown = position + new Vector3(0.42f, 3.65f, 0f) * scale;
            CreateBackdropBlock("Palm Leaves", root, crown,
                new Vector3(3.2f, 0.28f, 0.7f) * scale, leaves);
            CreateBackdropBlock("Palm Leaves", root, crown + new Vector3(0f, 0.08f, 0f),
                new Vector3(0.7f, 0.28f, 3.2f) * scale, leaves);
            CreateBackdropBlock("Palm Leaves Crown", root, crown + Vector3.up * 0.28f * scale,
                new Vector3(1.25f, 0.55f, 1.25f) * scale, leaves);
        }

        private static void CreateGround(Transform root, Material material)
        {
            // Kept below the court: it expands the world without covering the playing surface.
            CreateBackdropBlock("Outer Ground", root, new Vector3(0f, -0.23f, 4f),
                new Vector3(34f, 0.35f, 39f), material);
        }

        private static void CreateTerraces(
            Transform root,
            Material body,
            Material top,
            Material farBody,
            float heightScale)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 8.4f;
                for (int i = 0; i < 5; i++)
                {
                    float z = -5.5f + i * 4.5f;
                    float height = (0.65f + (i % 3) * 0.45f) * heightScale;
                    CreateBackdropBlock("Side Terrace", root, new Vector3(x, height * 0.5f - 0.05f, z),
                        new Vector3(3.1f, height, 3.6f), body);
                    CreateBackdropBlock("Side Top", root, new Vector3(x, height - 0.02f, z),
                        new Vector3(3.15f, 0.18f, 3.65f), top);
                    x += side * 0.65f;
                }
            }

            for (int x = -7; x <= 7; x += 2)
            {
                float height = (1.2f + (Mathf.Abs(x * 7) % 4) * 0.48f) * heightScale;
                CreateBackdropBlock("Far Hill", root, new Vector3(x, height * 0.5f - 0.05f, 14.8f),
                    new Vector3(2.15f, height, 4.2f), farBody);
                CreateBackdropBlock("Far Hill Top", root, new Vector3(x, height, 14.8f),
                    new Vector3(2.2f, 0.2f, 4.25f), top);
            }
        }

        private static void CreateBlockTree(
            Transform root,
            Vector3 position,
            float scale,
            Material wood,
            Material leaves)
        {
            CreateBackdropBlock("Tree Trunk", root, position + new Vector3(0f, 1.15f * scale, 0f),
                new Vector3(0.55f, 2.3f, 0.55f) * scale, wood);
            CreateBackdropBlock("Tree Crown", root, position + new Vector3(0f, 2.65f * scale, 0f),
                new Vector3(2.2f, 1.45f, 2f) * scale, leaves);
            CreateBackdropBlock("Tree Crown Top", root, position + new Vector3(0f, 3.55f * scale, 0f),
                new Vector3(1.35f, 0.55f, 1.35f) * scale, leaves);
        }

        private static void CreateCactus(Transform root, Vector3 position, float scale, Material material)
        {
            CreateBackdropBlock("Cactus", root, position + new Vector3(0f, 1.25f * scale, 0f),
                new Vector3(0.55f, 2.5f, 0.55f) * scale, material);
            CreateBackdropBlock("Cactus Arm", root, position + new Vector3(0.58f, 1.25f, 0f) * scale,
                new Vector3(0.75f, 0.45f, 0.48f) * scale, material);
            CreateBackdropBlock("Cactus Arm", root, position + new Vector3(-0.5f, 1.65f, 0f) * scale,
                new Vector3(0.65f, 0.42f, 0.48f) * scale, material);
        }

        private static void CreateCloud(Transform root, Vector3 position, float scale, Material material)
        {
            CreateBackdropBlock("Cloud", root, position, new Vector3(3.3f, 0.65f, 0.55f) * scale,
                material, false);
            CreateBackdropBlock("Cloud Puff", root, position + new Vector3(-0.65f, 0.45f, 0f) * scale,
                new Vector3(1.15f, 0.65f, 0.55f) * scale, material, false);
            CreateBackdropBlock("Cloud Puff", root, position + new Vector3(0.75f, 0.35f, 0f) * scale,
                new Vector3(1.45f, 0.75f, 0.55f) * scale, material, false);
        }

        private static Material CreateBackdropMaterial(
            Shader shader,
            string name,
            Color color,
            bool emissive = false)
        {
            Material material = CreateRuntimeMaterial(shader, name, color);
            material.SetFloat("_Glossiness", 0f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.7f);
            }
            return material;
        }

        private static void CreateBackdropBlock(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool castShadows = true)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            MeshRenderer renderer = block.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;
            Destroy(block.GetComponent<Collider>());
        }
    }
}
