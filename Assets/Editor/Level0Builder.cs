using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Rebuilds Level 0 from the text map below: opens the scene, lays out the
// side-on map, sets up the player's movement and circle of light, the map
// flip and the HUD, then saves. Edit the map and run it again.
//
// The map is seen from the side and gravity pulls down the screen. The
// tall wall in the middle can't be jumped, but its gap at the top is at
// floor level once the map flips.
// Run it from the menu: Tools > Level 0 > Build Level 0.
public static class Level0Builder
{
    internal const string ScenePath = "Assets/Scenes/Level0.unity";

    // # wall   . open space   S player start   E exit   * star
    // Row 0 is the top. Each character is one world unit. S and E are
    // placed at the centre of all tiles with that letter; each touching
    // group of * is one star. The player can jump about 3 tiles high.
    private static readonly string[] Layout =
    {
        "##################################",
        "#................................#",
        "#................................#",
        "#...............#................#",
        "#...............#................#",
        "#...............#........*.......#",
        "#...............#.....#######....#",
        "#...............#................#",
        "#...............#................#",
        "#...........#####................#",
        "#...............#................#",
        "#.......*.......#..........####..#",
        "#......####.....#................#",
        "#...............#.....####.......#",
        "#...............#................#",
        "#.........###...#................#",
        "#.....#...###...#.....###.....EE.#",
        "#SS...#...###...#.....###.....EE.#",
        "##################################",
    };

    private const string StarSpritePath = "Assets/Art/Star.png";
    private const string PlayerMaterialPath = "Assets/Physics/PlayerNoFriction.physicsMaterial2D";

    private const float PreviewSeconds = 5f;
    private const float RevealSeconds = 2f;
    private const int RevealCharges = 3;
    private const float SecondsBetweenFlips = 8f;
    private const float FlipWarningSeconds = 2f;

    // The circle the player can see in the dark, in world units.
    private const float VisionInnerRadius = 0.5f;
    private const float VisionOuterRadius = 2f;

    // Shared accelerated player physics used by every current and future level.
    private const float PlayerGravityScale = 3f;
    private const float PlayerMoveSpeed = 7.5f;
    private const float PlayerGroundAcceleration = 38f;
    private const float PlayerAirAcceleration = 12f;
    private const float PlayerFlatGroundBraking = 13f;
    private const float PlayerJumpSpeed = 13.2f;
    private const float PlayerMaxFallSpeed = 22f;
    private const float PlayerCoyoteSeconds = 0.1f;
    private const float PlayerJumpBufferSeconds = 0.12f;
    private const float PlayerMinimumGroundNormal = 0.45f;
    private const float PlayerBoxSize = 0.55f;

    private static readonly Vector2 ExitSize = new Vector2(1.4f, 2f);

    // Normal dark-play framing and the wider view earned by spending F light.
    private const float FollowCameraSize = 4f;
    private const float RevealCameraSize = 8f;
    private const float RevealCameraZoomSeconds = 0.25f;

    private static readonly Color DefaultWallColor = new Color(0.83f, 0.83f, 0.83f);
    private static readonly Color VisionColor = new Color(1f, 0.95f, 0.85f);
    private static readonly Color StarGlowColor = new Color(1f, 0.82f, 0.3f);

    [MenuItem("Tools/Level 0/Build Level 0")]
    private static void BuildMenuItem()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Build();
        }
    }

    internal static bool Build()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject floor = GameObject.Find("Floor");
        GameObject walls = GameObject.Find("Walls");
        GameObject player = GameObject.Find("Player");
        ExitTrigger exitTrigger = Object.FindAnyObjectByType<ExitTrigger>();
        GameObject exit = exitTrigger != null ? exitTrigger.gameObject : null;
        Camera camera = Camera.main;
        GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
        Sprite starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);

        if (floor == null || walls == null || player == null ||
            exit == null || camera == null || gameManager == null)
        {
            Debug.LogError(
                $"Level0Builder: {ScenePath} needs Floor, Walls, Player, an ExitTrigger, " +
                "Main Camera and GameManager objects."
            );
            return false;
        }

        if (starSprite == null)
        {
            Debug.LogError($"Level0Builder: no sprite found at {StarSpritePath}.");
            return false;
        }

        int width = Layout[0].Length;
        int height = Layout.Length;

        if (Layout.Any(row => row.Length != width))
        {
            Debug.LogError("Level0Builder: every layout row must be the same length.");
            return false;
        }

        SpriteRenderer floorRenderer = floor.GetComponent<SpriteRenderer>();
        Sprite squareSprite = floorRenderer.sprite;
        Material litMaterial = floorRenderer.sharedMaterial;
        Color wallColor = DefaultWallColor;

        SpriteRenderer existingWall = walls.GetComponentInChildren<SpriteRenderer>();
        if (existingWall != null)
        {
            wallColor = existingWall.color;
        }

        // Objects from the old version of the level.
        DestroyIfFound("Hazards");
        DestroyIfFound("Lantern");
        DestroyIfFound("Stars");

        // Everything under the map root turns when the maze flips.
        Transform map = FindOrCreate("Map").transform;
        map.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        map.localScale = Vector3.one;
        floor.transform.SetParent(map, true);
        walls.transform.SetParent(map, true);
        exit.transform.SetParent(map, true);

        // Walls. Their colliders merge into one shape so the player can't
        // snag on the seams between neighbouring blocks.
        for (int i = walls.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(walls.transform.GetChild(i).gameObject);
        }

        CompositeCollider2D composite = SetUpWallComposite(walls);

        List<RectInt> wallRects = MergeWallTiles(width, height);
        foreach (RectInt rect in wallRects)
        {
            Vector2 center = RectCenter(rect, width, height);
            GameObject wall = CreateSprite(
                "Wall", walls.transform, center, squareSprite, litMaterial,
                wallColor, 0
            );
            wall.transform.localScale = new Vector3(rect.width, rect.height, 1f);
            wall.AddComponent<BoxCollider2D>().compositeOperation =
                Collider2D.CompositeOperation.Merge;
        }

        composite.GenerateGeometry();

        // Stars
        Transform stars = new GameObject("Stars").transform;
        stars.SetParent(map, false);
        int starCount = 0;

        foreach (List<Vector2Int> group in TileGroups('*'))
        {
            Vector2 position = Vector2.zero;
            foreach (Vector2Int tile in group)
            {
                position += TileCenter(tile, width, height);
            }
            position /= group.Count;

            GameObject star = CreateSprite(
                "Star", stars, position, starSprite, litMaterial, Color.white, 6
            );
            star.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            CircleCollider2D trigger = star.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.6f;

            star.AddComponent<StarCollectible>();
            star.AddComponent<PulsingGlow>().Configure(StarGlowColor, 1.2f, 0.4f, 0.9f);
            starCount++;
        }

        // Player, exit and floor
        if (TryGetMarker('S', width, height, out Vector2 startPosition))
        {
            player.transform.position = startPosition;
        }

        if (TryGetMarker('E', width, height, out Vector2 exitPosition))
        {
            exit.transform.position = exitPosition;
        }

        // A door standing on the floor.
        exit.transform.localScale = new Vector3(ExitSize.x, ExitSize.y, 1f);

        // No glow on the exit: the player has to find it in the dark.
        PulsingGlow exitGlow = exit.GetComponent<PulsingGlow>();
        if (exitGlow != null)
        {
            Object.DestroyImmediate(exitGlow);
        }

        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(width, height, 1f);

        ConfigurePlayer(player);

        Light2D visionLight = BuildVisionLight(player.transform);
        MapFlipController mapFlip = ConfigureMapFlip(gameManager, map, player, visionLight);
        CameraFollow cameraFollow = ConfigureCamera(camera, player, floorRenderer, mapFlip);
        ConfigureGameManager(gameManager, mapFlip, cameraFollow);

        // Also frames the camera around the maze.
        if (!HudBuilder.BuildHud())
        {
            return false;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            $"Level0Builder: built a {width}x{height} map with {wallRects.Count} " +
            $"walls and {starCount} stars, and saved {ScenePath}."
        );
        return true;
    }

    private static CompositeCollider2D SetUpWallComposite(GameObject walls)
    {
        // The composite needs a body; a static one never moves on its own,
        // only when the map flip turns it.
        Rigidbody2D body = walls.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            body = walls.AddComponent<Rigidbody2D>();
        }
        body.bodyType = RigidbodyType2D.Static;

        CompositeCollider2D composite = walls.GetComponent<CompositeCollider2D>();
        if (composite == null)
        {
            composite = walls.AddComponent<CompositeCollider2D>();
        }
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        return composite;
    }

    private static void ConfigurePlayer(GameObject player)
    {
        // Removes the old wall bump script, which no longer exists.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(player);

        // A box stands squarely on ledges, where a circle would roll off.
        CircleCollider2D circle = player.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Object.DestroyImmediate(circle);
        }

        BoxCollider2D box = player.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = player.AddComponent<BoxCollider2D>();
        }
        float scale = player.transform.lossyScale.x;
        box.size = Vector2.one * (PlayerBoxSize / scale);
        box.offset = Vector2.zero;

        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = PlayerGravityScale;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        // No friction, so the player slides down walls instead of sticking.
        body.sharedMaterial = LoadOrCreatePlayerMaterial();

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            movement = player.AddComponent<PlayerMovement>();
        }

        SerializedObject serialized = new SerializedObject(movement);
        serialized.FindProperty("moveSpeed").floatValue = PlayerMoveSpeed;
        serialized.FindProperty("groundAcceleration").floatValue = PlayerGroundAcceleration;
        serialized.FindProperty("airAcceleration").floatValue = PlayerAirAcceleration;
        serialized.FindProperty("flatGroundBraking").floatValue = PlayerFlatGroundBraking;
        serialized.FindProperty("jumpSpeed").floatValue = PlayerJumpSpeed;
        serialized.FindProperty("maxFallSpeed").floatValue = PlayerMaxFallSpeed;
        serialized.FindProperty("coyoteSeconds").floatValue = PlayerCoyoteSeconds;
        serialized.FindProperty("jumpBufferSeconds").floatValue = PlayerJumpBufferSeconds;
        serialized.FindProperty("minimumGroundNormal").floatValue = PlayerMinimumGroundNormal;
        serialized.ApplyModifiedProperties();
    }

    private static PhysicsMaterial2D LoadOrCreatePlayerMaterial()
    {
        PhysicsMaterial2D material =
            AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PlayerMaterialPath);

        if (material != null)
        {
            return material;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Physics"))
        {
            AssetDatabase.CreateFolder("Assets", "Physics");
        }

        material = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
        AssetDatabase.CreateAsset(material, PlayerMaterialPath);
        return material;
    }

    // The circle of light the player carries. It's a child of the player
    // so it follows them through the flip.
    private static Light2D BuildVisionLight(Transform player)
    {
        const string name = "Vision Light";
        Transform old = player.Find(name);
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(player, false);

        // Cancel the player's scale so the radii are in world units.
        Vector3 scale = player.lossyScale;
        lightObject.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f);

        Light2D light = lightObject.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = VisionColor;
        light.intensity = 1f;
        light.pointLightInnerRadius = VisionInnerRadius;
        light.pointLightOuterRadius = VisionOuterRadius;
        return light;
    }

    private static MapFlipController ConfigureMapFlip(
        GameManager gameManager, Transform map, GameObject player, Light2D visionLight)
    {
        MapFlipController mapFlip = gameManager.GetComponent<MapFlipController>();
        if (mapFlip == null)
        {
            mapFlip = gameManager.gameObject.AddComponent<MapFlipController>();
        }

        SerializedObject serialized = new SerializedObject(mapFlip);
        serialized.FindProperty("mapRoot").objectReferenceValue = map;
        serialized.FindProperty("player").objectReferenceValue = player.GetComponent<Rigidbody2D>();
        serialized.FindProperty("visionLight").objectReferenceValue = visionLight;
        serialized.FindProperty("secondsBetweenFlips").floatValue = SecondsBetweenFlips;
        serialized.FindProperty("warningSeconds").floatValue = FlipWarningSeconds;
        serialized.ApplyModifiedProperties();
        return mapFlip;
    }

    private static CameraFollow ConfigureCamera(
        Camera camera, GameObject player, SpriteRenderer floor, MapFlipController mapFlip)
    {
        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<CameraFollow>();
        }

        SerializedObject serialized = new SerializedObject(follow);
        serialized.FindProperty("target").objectReferenceValue = player.transform;
        serialized.FindProperty("mapBounds").objectReferenceValue = floor;
        serialized.FindProperty("mapFlip").objectReferenceValue = mapFlip;
        serialized.FindProperty("followSize").floatValue = FollowCameraSize;
        serialized.FindProperty("revealSize").floatValue = RevealCameraSize;
        serialized.FindProperty("revealZoomSeconds").floatValue = RevealCameraZoomSeconds;
        serialized.FindProperty("topBarShare").floatValue =
            HudBuilder.TopBarHeight / HudBuilder.ReferenceHeight;
        serialized.FindProperty("bottomBarShare").floatValue =
            HudBuilder.BottomBarHeight / HudBuilder.ReferenceHeight;
        serialized.ApplyModifiedProperties();
        return follow;
    }

    private static void ConfigureGameManager(
        GameManager gameManager, MapFlipController mapFlip, CameraFollow cameraFollow)
    {
        SerializedObject serialized = new SerializedObject(gameManager);
        serialized.FindProperty("previewSeconds").floatValue = PreviewSeconds;
        serialized.FindProperty("revealSeconds").floatValue = RevealSeconds;
        serialized.FindProperty("revealCharges").intValue = RevealCharges;
        serialized.FindProperty("darkIntensity").floatValue = 0f;
        serialized.FindProperty("mapFlip").objectReferenceValue = mapFlip;
        serialized.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        serialized.ApplyModifiedProperties();
    }

    // Greedily merges wall tiles into rectangles so the maze uses a few
    // large colliders instead of hundreds of tiny ones.
    private static List<RectInt> MergeWallTiles(int width, int height)
    {
        bool[,] used = new bool[width, height];
        List<RectInt> rects = new List<RectInt>();

        bool IsFreeWall(int x, int y) => Layout[y][x] == '#' && !used[x, y];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!IsFreeWall(x, y))
                {
                    continue;
                }

                int rectWidth = 1;
                while (x + rectWidth < width && IsFreeWall(x + rectWidth, y))
                {
                    rectWidth++;
                }

                int rectHeight = 1;
                while (y + rectHeight < height &&
                       Enumerable.Range(x, rectWidth).All(cx => IsFreeWall(cx, y + rectHeight)))
                {
                    rectHeight++;
                }

                for (int dy = 0; dy < rectHeight; dy++)
                {
                    for (int dx = 0; dx < rectWidth; dx++)
                    {
                        used[x + dx, y + dy] = true;
                    }
                }

                rects.Add(new RectInt(x, y, rectWidth, rectHeight));
            }
        }

        return rects;
    }

    private static IEnumerable<Vector2Int> TilesWith(char marker)
    {
        for (int y = 0; y < Layout.Length; y++)
        {
            for (int x = 0; x < Layout[y].Length; x++)
            {
                if (Layout[y][x] == marker)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }
    }

    // Splits the tiles with this letter into groups that touch each other.
    private static List<List<Vector2Int>> TileGroups(char marker)
    {
        HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(TilesWith(marker));
        List<List<Vector2Int>> groups = new List<List<Vector2Int>>();
        Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        while (remaining.Count > 0)
        {
            Vector2Int first = remaining.First();
            remaining.Remove(first);

            List<Vector2Int> group = new List<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(first);

            while (queue.Count > 0)
            {
                Vector2Int tile = queue.Dequeue();
                group.Add(tile);

                foreach (Vector2Int step in steps)
                {
                    if (remaining.Remove(tile + step))
                    {
                        queue.Enqueue(tile + step);
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private static bool TryGetMarker(char marker, int width, int height, out Vector2 position)
    {
        List<Vector2Int> tiles = TilesWith(marker).ToList();
        position = Vector2.zero;

        if (tiles.Count == 0)
        {
            Debug.LogWarning($"Level0Builder: layout has no '{marker}' tile.");
            return false;
        }

        foreach (Vector2Int tile in tiles)
        {
            position += TileCenter(tile, width, height);
        }

        position /= tiles.Count;
        return true;
    }

    // Row 0 of the layout is the top of the maze, and the maze is centred on (0, 0).
    private static Vector2 TileCenter(Vector2Int tile, int width, int height)
    {
        return new Vector2(
            tile.x - (width - 1) * 0.5f,
            (height - 1) * 0.5f - tile.y
        );
    }

    private static Vector2 RectCenter(RectInt rect, int width, int height)
    {
        Vector2 topLeft = TileCenter(rect.position, width, height);
        return topLeft + new Vector2((rect.width - 1) * 0.5f, -(rect.height - 1) * 0.5f);
    }

    private static GameObject CreateSprite(
        string name, Transform parent, Vector2 position, Sprite sprite,
        Material material, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject existing = GameObject.Find(name);
        return existing != null ? existing : new GameObject(name);
    }

    private static void DestroyIfFound(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }
}
