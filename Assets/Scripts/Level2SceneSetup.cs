using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Level 2 starts from the unmodified Level 1 scene. All layout coordinates live
// in one resource, shared by the editor baker and non-Unity geometry checks.
[DefaultExecutionOrder(-10000)]
public sealed class Level2SceneSetup : MonoBehaviour
{
    [Serializable] public sealed class Block
    {
        public string name, kind;
        public float x, y, w, h, angle, speed;
    }
    [Serializable] public sealed class Lift
    {
        public string name;
        public float x, y, w, h, range, period, phase;
    }
    [Serializable] public sealed class Pickup
    {
        public string name;
        public float x, y;
    }
    [Serializable] public sealed class Layout
    {
        public int revision;
        public float width, height, spawnX, spawnY, exitX, exitY;
        public Block[] blocks;
        public Lift[] lifts;
        public Pickup[] stars;
    }

    private Transform generated, wallRoot;
    private Sprite square;
    private Material litMaterial;
    private PhysicsMaterial2D grip;
    private Color wallColor = new Color(.8301887f, .8301887f, .8301887f);

    private void Awake()
    {
        if (SceneManager.GetActiveScene().name == "Level2") Rebuild();
    }

    public void Rebuild()
    {
        TextAsset data = Resources.Load<TextAsset>("Level2RingLayout");
        Layout layout = data != null ? JsonUtility.FromJson<Layout>(data.text) : null;
        GameObject map = GameObject.Find("Map");
        GameObject walls = GameObject.Find("Walls");
        GameObject floor = GameObject.Find("Floor");
        GameObject player = GameObject.Find("Player");
        ExitTrigger exit = FindFirstObjectByType<ExitTrigger>();
        if (layout == null || layout.stars == null || layout.stars.Length != 5 ||
            map == null || walls == null || floor == null || player == null || exit == null)
        {
            Debug.LogError("Ring Exchange requires Level 1's scene rig and Level2RingLayout with five stars.");
            return;
        }

        map.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        map.transform.localScale = Vector3.one;
        wallRoot = walls.transform;
        SpriteRenderer background = floor.GetComponent<SpriteRenderer>();
        square = background.sprite;
        litMaterial = background.sharedMaterial;
        SpriteRenderer originalWall = walls.GetComponentInChildren<SpriteRenderer>();
        if (originalWall != null) wallColor = originalWall.color;

        // Deactivate before deferred destruction, so old colliders and pickups
        // cannot participate in GameManager.Awake or the first physics step.
        for (int i = wallRoot.childCount - 1; i >= 0; --i) Remove(wallRoot.GetChild(i).gameObject);
        Transform old = map.transform.Find("L2 Generated Geometry");
        if (old != null) Remove(old.gameObject);
        old = map.transform.Find("Stars");
        if (old != null) Remove(old.gameObject);
        generated = new GameObject("L2 Generated Geometry").transform;
        generated.SetParent(map.transform, false);
        grip = Resources.Load<PhysicsMaterial2D>("Level2Rolling");
        if (grip == null) grip = new PhysicsMaterial2D("Level 2 Rolling") { friction = .65f, bounciness = 0f };

        // Preserve Level 1's materials, background and player appearance.
        floor.transform.localPosition = Vector3.zero;
        floor.transform.localScale = new Vector3(layout.width, layout.height, 1f);
        ConfigureBall(player, layout);
        exit.transform.SetParent(map.transform, false);
        exit.transform.localPosition = new Vector3(layout.exitX, layout.exitY, 0f);
        exit.transform.localScale = new Vector3(1.4f, 2f, 1f);
        foreach (Block b in layout.blocks) AddBlock(b);
        foreach (Lift lift in layout.lifts) AddLift(lift);
        foreach (Pickup star in layout.stars) AddStar(star);

        CompositeCollider2D composite = wallRoot.GetComponent<CompositeCollider2D>();
        if (composite != null) composite.GenerateGeometry();
        Physics2D.SyncTransforms();
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.orthographicSize = Mathf.Max(layout.height * .5f + 2.7f,
                layout.width * .5f / Mathf.Max(.1f, camera.aspect) + 1.2f);
        }
    }

    private void ConfigureBall(GameObject player, Layout layout)
    {
        player.transform.position = new Vector3(layout.spawnX, layout.spawnY, 0f);
        player.transform.rotation = Quaternion.identity;
        // Do not override the original sprite, tint, material or .65 scale.
        Transform mark = player.transform.Find("Rolling indicator");
        if (mark != null) Remove(mark.gameObject);
        PlayerMovement original = player.GetComponent<PlayerMovement>();
        if (original != null) { original.enabled = false; Remove(original); }
        BoxCollider2D box = player.GetComponent<BoxCollider2D>();
        if (box != null) { box.enabled = false; Remove(box); }
        CircleCollider2D circle = player.GetComponent<CircleCollider2D>();
        if (circle == null) circle = player.AddComponent<CircleCollider2D>();
        circle.radius = .275f / Mathf.Max(.01f, player.transform.lossyScale.x);
        circle.sharedMaterial = grip;
        Rigidbody2D body = player.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 3f;
        body.constraints = RigidbodyConstraints2D.None;
        body.linearDamping = .06f;
        body.angularDamping = .5f;
        body.sharedMaterial = grip;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        if (player.GetComponent<Level2BallMotor>() == null) player.AddComponent<Level2BallMotor>();

        // Level 1 light's .65 player scale * 1.5384616 child scale = 1.
        // Keep the light fixed in world size even if player scale is adjusted.
        Transform vision = player.transform.Find("Vision Light");
        if (vision != null)
        {
            float scale = Mathf.Max(.01f, Mathf.Abs(player.transform.lossyScale.x));
            vision.localScale = new Vector3(1f / scale, 1f / scale, 1f);
            Light2D light = vision.GetComponent<Light2D>();
            if (light != null)
            {
                light.pointLightInnerRadius = .5f;
                light.pointLightOuterRadius = 2f;
            }
        }
    }

    private GameObject Shape(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = square;
        renderer.sharedMaterial = litMaterial;
        renderer.color = color;
        return go;
    }

    private void AddBlock(Block block)
    {
        bool conveyor = block.kind == "conveyor";
        Color color = conveyor ? Color.Lerp(wallColor, new Color(.56f, .67f, .71f), .45f) : wallColor;
        GameObject go = Shape(block.name, conveyor ? generated : wallRoot,
            new Vector2(block.x, block.y), new Vector2(block.w, block.h), color);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, block.angle);
        BoxCollider2D box = go.AddComponent<BoxCollider2D>();
        box.sharedMaterial = grip;
        if (!conveyor)
        {
            box.compositeOperation = Collider2D.CompositeOperation.Merge;
            return;
        }
        box.usedByEffector = true;
        SurfaceEffector2D belt = go.AddComponent<SurfaceEffector2D>();
        belt.speed = block.speed;
        belt.speedVariation = 0f;
        // Keep the effector as belt metadata for the animated treads. The
        // ball motor applies controlled drive so steering/jumping always work.
        belt.useContactForce = false;
        belt.forceScale = 0f;
        belt.useBounce = false;
        for (float x = -.4f; x < .45f; x += .65f / block.w)
        {
            GameObject tread = Shape("Belt tread", go.transform, new Vector2(x, 0f),
                new Vector2(.20f / block.w, .13f), new Color(.22f, .26f, .28f));
            tread.GetComponent<SpriteRenderer>().sortingOrder = 2;
            tread.AddComponent<Level2BeltMark>();
        }
    }

    private void AddLift(Lift lift)
    {
        GameObject go = Shape(lift.name, generated, new Vector2(lift.x, lift.y),
            new Vector2(lift.w, lift.h), Color.Lerp(wallColor, new Color(.56f, .67f, .71f), .45f));
        // Movement in world units must not inherit the sprite's non-unit scale.
        go.AddComponent<BoxCollider2D>().sharedMaterial = grip;
        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        Level2MotionSurface motion = go.AddComponent<Level2MotionSurface>();
        motion.SetShuttle(new Vector2(0f, lift.range), lift.period, lift.phase);
    }

    private void AddStar(Pickup pickup)
    {
        GameObject star = new GameObject(pickup.name);
        star.transform.SetParent(generated, false);
        star.transform.localPosition = new Vector2(pickup.x, pickup.y);
        star.transform.localScale = Vector3.one * .7f;
        SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
        renderer.sprite = Resources.Load<Sprite>("Level2Star");
        renderer.sharedMaterial = litMaterial;
        renderer.color = Color.white;
        renderer.sortingOrder = 6;
        CircleCollider2D trigger = star.AddComponent<CircleCollider2D>();
        trigger.radius = .6f;
        trigger.isTrigger = true;
        star.AddComponent<StarCollectible>();
        star.AddComponent<PulsingGlow>().Configure(new Color(1f, .82f, .3f), 1.2f, .4f, .9f);
    }

    private static void Remove(UnityEngine.Object target)
    {
        if (Application.isPlaying)
        {
            if (target is GameObject go) go.SetActive(false);
            Destroy(target);
        }
        else DestroyImmediate(target);
    }
}
