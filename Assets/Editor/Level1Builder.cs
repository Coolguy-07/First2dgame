using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds Assets/Scenes/Level1.unity from scratch: Hollow Grove, Ash, monsters,
// checkpoints, the Dusk Gate, UI, audio and build settings.
public static class Level1Builder
{
    const string ScenePath = "Assets/Scenes/Level1.unity";

    static SpriteBank art;
    static Font uiFont;
    static Material warmGlowMat;
    static Material coolGlowMat;
    static Material violetGlowMat;
    static Material swingMat;
    static Material acidMat;

    public static string BuildAll(Dictionary<string, object> args)
    {
        art = PixelArt.Generate();
        CreateMaterials();
        ConfigureGpuPipeline();
        int groundLayer = EnsureLayer("Ground");
        int enemyLayer = EnsureLayer("Enemy");

        if (EditorSceneManager.GetActiveScene().isDirty)
            EditorSceneManager.SaveOpenScenes();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var world = new GameObject("World").transform;

        var follow = CreateCamera(out Camera cam);
        CreateSky(cam.transform);
        CreateParallax(cam.transform);
        CreatePostFx();
        CreateAmbientParticles(cam.transform);
        CreateGround(world, groundLayer);
        CreatePlatforms(world, groundLayer);
        CreateDressing(world, cam.transform);
        var player = CreatePlayer(groundLayer, enemyLayer);
        CreateMonsters(world, enemyLayer, groundLayer);
        CreateDialogueTriggers(world);
        CreateCheckpoint(world, "Checkpoint_A", 18.2f);
        CreateCheckpoint(world, "Checkpoint_B", 38.2f);
        var goal = CreatePortal(world);

        follow.target = player.transform;

        CreateAudio();
        var ui = CreateUI();
        var gm = CreateGameManager(player);

        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(ui);
        if (goal != null) EditorUtility.SetDirty(goal);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new Exception("failed to save " + ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();

        var monsters = UnityEngine.Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);
        return string.Format(
            "Level1 built\nscene: {0}\nlayers: Ground={1} Enemy={2}\nmonsters: {3}\nplayer: {4}\nUI hearts: {5}",
            ScenePath, groundLayer, enemyLayer, monsters.Length,
            player.transform.position, ui.hearts != null ? ui.hearts.Length : 0);
    }

    static int EnsureLayer(string name)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) throw new Exception("TagManager.asset not found");
        var so = new SerializedObject(assets[0]);
        var layers = so.FindProperty("layers");
        int free = -1;

        for (int i = 8; i < layers.arraySize; i++)
        {
            var el = layers.GetArrayElementAtIndex(i);
            if (el.stringValue == name) return i;
            if (free < 0 && string.IsNullOrEmpty(el.stringValue)) free = i;
        }
        if (free < 0) throw new Exception("no free layer slots for " + name);

        layers.GetArrayElementAtIndex(free).stringValue = name;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        return free;
    }

    // ------------------------------------------------------------ camera

    static CameraFollow CreateCamera(out Camera cam)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(1f, 2.5f, -10f);

        cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x24, 0x1F, 0x33, 255);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 100f;
        cam.allowHDR = true;
        cam.allowMSAA = true;

        var camData = cam.GetUniversalAdditionalCameraData();
        camData.renderPostProcessing = true;
        camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camData.antialiasingQuality = AntialiasingQuality.High;

        go.AddComponent<AudioListener>();

        var follow = go.AddComponent<CameraFollow>();
        follow.smoothTime = 0.16f;
        follow.minX = 0.9f;
        follow.maxX = 55.1f;
        follow.fixedY = 2.5f;
        follow.normalSize = 5f;
        follow.bossSize = 4.25f;
        return follow;
    }

    static void CreateSky(Transform cam)
    {
        var sky = new GameObject("Sky");
        sky.transform.SetParent(cam, false);
        sky.transform.localPosition = new Vector3(0f, 0f, 10f);
        sky.transform.localScale = new Vector3(160f, 1.6f, 1f);
        var sr = sky.AddComponent<SpriteRenderer>();
        sr.sprite = art.sky;
        sr.sortingOrder = -100;

        var stars = new GameObject("Stars");
        stars.transform.SetParent(cam, false);
        stars.transform.localPosition = new Vector3(0f, 0f, 9.8f);
        stars.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
        var ssr = stars.AddComponent<SpriteRenderer>();
        ssr.sprite = art.stars;
        ssr.sortingOrder = -99;

        var moon = new GameObject("Moon");
        moon.transform.SetParent(cam, false);
        moon.transform.localPosition = new Vector3(5.2f, 2.9f, 9.9f);
        moon.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
        var mr = moon.AddComponent<SpriteRenderer>();
        mr.sprite = art.moon;
        mr.sortingOrder = -98;
        mr.color = new Color(1.45f, 1.35f, 1.1f, 1f);
        AddGlow("MoonGlow", moon.transform, new Vector3(0f, 0f, 0.05f), 3.7f,
                new Color(0.8f, 0.72f, 0.58f, 1f), warmGlowMat, -99, true);
    }

    static void CreateParallax(Transform cam)
    {
        var hills = new GameObject("Parallax_Hills");
        hills.transform.position = new Vector3(30f, 1.6f, 0f);
        hills.transform.localScale = new Vector3(6f, 1f, 1f);
        var hr = hills.AddComponent<SpriteRenderer>();
        hr.sprite = art.hills;
        hr.sortingOrder = -94;
        var hp = hills.AddComponent<ParallaxLayer>();
        hp.cameraTransform = cam;
        hp.factor = 0.05f;

        var trees = new GameObject("Parallax_Trees");
        trees.transform.position = new Vector3(30f, 1.5f, 0f);
        trees.transform.localScale = new Vector3(6f, 0.85f, 1f);
        var tr = trees.AddComponent<SpriteRenderer>();
        tr.sprite = art.trees;
        tr.sortingOrder = -92;
        var tp = trees.AddComponent<ParallaxLayer>();
        tp.cameraTransform = cam;
        tp.factor = 0.14f;

        var foliage = new GameObject("Parallax_Foliage");
        foliage.transform.position = new Vector3(30f, 1f, 0f);
        foliage.transform.localScale = new Vector3(5f, 0.8f, 1f);
        var fr = foliage.AddComponent<SpriteRenderer>();
        fr.sprite = art.foliage;
        fr.sortingOrder = -90;
        var fp = foliage.AddComponent<ParallaxLayer>();
        fp.cameraTransform = cam;
        fp.factor = 0.26f;
    }

    // ------------------------------------------------------------ terrain

    static void CreateGround(Transform parent, int layer)
    {
        var root = new GameObject("Ground").transform;
        root.SetParent(parent, false);
        GroundSegment(root, "Ground_A", -8f, 14f, layer);
        GroundSegment(root, "Ground_B", 17f, 34f, layer);
        GroundSegment(root, "Ground_C", 37f, 66f, layer);

        var wall = new GameObject("LeftBoundary");
        wall.transform.SetParent(root, false);
        wall.layer = layer;
        wall.transform.position = new Vector3(-6.5f, 1.5f, 0f);
        var wc = wall.AddComponent<BoxCollider2D>();
        wc.size = new Vector2(1f, 3f);
    }

    static void GroundSegment(Transform parent, string name, float x0, float x1, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        float w = x1 - x0;
        go.transform.position = new Vector3((x0 + x1) * 0.5f, -0.5f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.ground;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(w, 1f);
        sr.sortingOrder = -20;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(w, 1f);

        var soil = new GameObject(name + "_Soil");
        soil.transform.SetParent(parent, false);
        soil.transform.position = new Vector3((x0 + x1) * 0.5f, -1.5f, 0f);
        var ssr = soil.AddComponent<SpriteRenderer>();
        ssr.sprite = art.soil;
        ssr.drawMode = SpriteDrawMode.Tiled;
        ssr.tileMode = SpriteTileMode.Continuous;
        ssr.size = new Vector2(w, 3f);
        ssr.sortingOrder = -21;
    }

    static void CreatePlatforms(Transform parent, int layer)
    {
        var root = new GameObject("Platforms").transform;
        root.SetParent(parent, false);
        Platform(root, "Platform_A", 22f, 1.5f, 3f, layer);
        Platform(root, "Platform_B", 35.5f, 1.2f, 2.2f, layer);
    }

    static void Platform(Transform parent, string name, float cx, float topY, float width, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.transform.position = new Vector3(cx, topY - 0.25f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.platform;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = new Vector2(width, 0.5f);
        sr.sortingOrder = -18;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width, 0.5f);
    }

    // ------------------------------------------------------------ dressing

    static void CreateDressing(Transform parent, Transform cam)
    {
        // Foreground grass bands hide the hero's boots and frame the ground.
        var fg = new GameObject("Foreground").transform;
        fg.SetParent(parent, false);
        float[] xs = { 3f, 24f, 39f, 55f };
        for (int i = 0; i < xs.Length; i++)
        {
            var go = new GameObject("ForegroundGrass_" + (i + 1));
            go.transform.SetParent(fg, false);
            go.transform.position = new Vector3(xs[i], -0.62f, 0f);
            go.transform.localScale = new Vector3(1f, 1f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.grass;
            sr.sortingOrder = 30;
            sr.color = new Color(0.92f, 0.9f, 1f, 1f);
        }

        // Low mist wisps ride with the camera so the screen never feels static.
        var mist = new GameObject("Mist").transform;
        mist.SetParent(cam, false);
        mist.localPosition = Vector3.zero;
        Vector3[] mpos =
        {
            new Vector3(-5.6f, 0.4f, 5.5f),
            new Vector3(0.2f, 1.8f, 5.5f),
            new Vector3(5.7f, 0.1f, 5.5f)
        };
        for (int i = 0; i < mpos.Length; i++)
        {
            var go = new GameObject("Mist_" + (i + 1));
            go.transform.SetParent(mist, false);
            go.transform.localPosition = mpos[i];
            go.transform.localScale = new Vector3(1.7f, 1.25f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.mist;
            sr.sortingOrder = 3;
            sr.color = new Color(0.82f, 0.78f, 1f, 0.75f);
            var pulse = go.AddComponent<GlowPulse>();
            pulse.speed = 0.22f;
            pulse.amount = 0.22f;
            pulse.scaleAmount = 0.02f;
        }

        // Ruined arch marks the arena threshold; braziers answer it.
        var arch = new GameObject("ArenaArch");
        arch.transform.SetParent(parent, false);
        arch.transform.position = new Vector3(49.4f, 1.85f, 0f);
        arch.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
        var asr = arch.AddComponent<SpriteRenderer>();
        asr.sprite = art.arch;
        asr.sortingOrder = 3;
        asr.color = new Color(0.82f, 0.78f, 0.95f, 1f);

        AddArenaBrazier(parent, 49.7f);
        AddArenaBrazier(parent, 59.3f);
    }

    static void AddArenaBrazier(Transform parent, float x)
    {
        var post = new GameObject("ArenaBrazier");
        post.transform.SetParent(parent, false);
        post.transform.position = new Vector3(x, 0f, 0f);

        var bowl = new GameObject("Bowl");
        bowl.transform.SetParent(post.transform, false);
        bowl.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        bowl.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
        var sr = bowl.AddComponent<SpriteRenderer>();
        sr.sprite = art.lantern;
        sr.sortingOrder = 6;
        sr.color = new Color(2.2f, 1.5f, 0.72f, 1f);

        AddGlow("BrazierGlow", bowl.transform, new Vector3(0f, 0.2f, 0f), 3.1f,
                new Color(1f, 0.78f, 0.42f, 1f), warmGlowMat, 5, true);
        AddBrazier(post.transform, new Vector3(0f, 1.15f, 0f));
    }

    // ------------------------------------------------------------ actors

    static GameObject CreatePlayer(int groundLayer, int enemyLayer)
    {
        var go = new GameObject("Player");
        go.tag = "Player";
        go.transform.position = new Vector3(1f, 0.75f, 0f);

        // The sprite lives on a child so PlayerController can bob/lean the art
        // without ever writing the physics body's own transform.
        var body = new GameObject("Body");
        body.transform.SetParent(go.transform, false);
        var sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = art.hero;
        sr.sortingOrder = 10;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.55f, 1.3f);
        col.offset = new Vector2(0f, -0.1f);

        var pc = go.AddComponent<PlayerController>();
        pc.bodyRenderer = sr;
        pc.swingSprite = art.swing;
        pc.swingMaterial = swingMat;
        pc.groundMask = 1 << groundLayer;
        pc.enemyMask = 1 << enemyLayer;
        pc.jumpVelocity = 13.5f;
        pc.gravityScale = 3f;
        pc.feetOffset = 0.72f;

        // Faint warm aura so the darkest sprite on screen still pops against
        // the violet mid-ground (picked up by bloom).
        AddGlow("HeroGlow", go.transform, new Vector3(0f, 0.12f, 0.6f), 1.75f,
                new Color(0.72f, 0.5f, 0.24f, 1f), warmGlowMat, 9, true);
        return go;
    }

    static void CreateMonsters(Transform parent, int enemyLayer, int groundLayer)
    {
        var root = new GameObject("Monsters").transform;
        root.SetParent(parent, false);

        // Act I — the waking wood
        Slime(root, "Slime_01", 5.2f, 0.28f, 3.6f, 7.4f, enemyLayer);
        Slime(root, "Slime_02", 10.2f, 0.28f, 8.2f, 12.6f, enemyLayer);
        Stalker(root, "Stalker_01", 12.9f, 0.45f, 11.6f, 13.8f, enemyLayer);
        Slime(root, "Slime_03", 21.0f, 0.28f, 18.9f, 23.4f, enemyLayer);

        // Act II — the deep grove
        Spitter(root, "Spitter_01", 25.4f, 0.53f, 23.5f, 27.2f, enemyLayer, groundLayer);
        Stalker(root, "Stalker_02", 40.6f, 0.45f, 38.9f, 42.4f, enemyLayer);
        Spitter(root, "Spitter_02", 43.8f, 0.53f, 42.4f, 45.6f, enemyLayer, groundLayer);

        // Act III — Thorne holds the gate
        Boss(root, "Thorne", 55.0f, 0.69f, 50.2f, 59.2f, enemyLayer);
    }

    static void Slime(Transform parent, string name, float x, float y, float left, float right, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.transform.position = new Vector3(x, y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.slime;
        sr.sortingOrder = 8;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.42f);
        col.offset = new Vector2(0f, -0.02f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.useFullKinematicContacts = true;

        var m = go.AddComponent<Monster>();
        m.displayName = "Shade";
        m.kind = Monster.Kind.Slime;
        m.maxHealth = 1;
        m.contactDamage = 1;
        m.moveSpeed = 1.4f;
        m.leftX = left;
        m.rightX = right;
        m.isBrute = false;
        m.fxMaterial = coolGlowMat;
    }

    static void Stalker(Transform parent, string name, float x, float y, float left, float right, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.transform.position = new Vector3(x, y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.stalker;
        sr.sortingOrder = 8;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.95f, 0.58f);
        col.offset = new Vector2(0f, -0.03f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.useFullKinematicContacts = true;

        var m = go.AddComponent<Monster>();
        m.displayName = "Stalker";
        m.kind = Monster.Kind.Stalker;
        m.maxHealth = 2;
        m.contactDamage = 1;
        m.moveSpeed = 1.1f;
        m.leftX = left;
        m.rightX = right;
        m.deathDuration = 0.38f;
        m.fxMaterial = coolGlowMat;
    }

    static void Spitter(Transform parent, string name, float x, float y, float left, float right, int layer, int groundLayer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.transform.position = new Vector3(x, y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.spitter;
        sr.sortingOrder = 8;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.75f, 0.82f);
        col.offset = new Vector2(0f, -0.06f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.useFullKinematicContacts = true;

        var m = go.AddComponent<Monster>();
        m.displayName = "Spitter";
        m.kind = Monster.Kind.Slime;
        m.maxHealth = 2;
        m.contactDamage = 1;
        m.moveSpeed = 0.8f;
        m.leftX = left;
        m.rightX = right;
        m.deathDuration = 0.42f;
        m.fxMaterial = acidMat;

        var mr = go.AddComponent<MonsterRanged>();
        mr.boltSprite = art.acid;
        mr.boltMaterial = acidMat;
        mr.groundMask = 1 << groundLayer;
        mr.range = 8.5f;
        mr.cooldown = 2.8f;
        mr.windup = 0.7f;
        mr.projectileSpeed = 7f;
    }

    static void Boss(Transform parent, string name, float x, float y, float left, float right, int layer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.layer = layer;
        go.transform.position = new Vector3(x, y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.brute;
        sr.sortingOrder = 8;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.05f, 1.2f);
        col.offset = new Vector2(0f, -0.08f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.useFullKinematicContacts = true;

        var m = go.AddComponent<Monster>();
        m.displayName = "Thorne";
        m.kind = Monster.Kind.Boss;
        m.maxHealth = 6;
        m.contactDamage = 1;
        m.moveSpeed = 1.2f;
        m.leftX = left;
        m.rightX = right;
        m.isBrute = true;
        m.deathDuration = 0.85f;
        m.phase2Health = 3;
        m.slamWindup = 0.7f;
        m.chargeWindup = 0.5f;
        m.fxMaterial = warmGlowMat;
        m.waveSprite = art.wave;
        m.waveMaterial = warmGlowMat;
    }

    // ------------------------------------------------------------ dialogue

    static void CreateDialogueTriggers(Transform parent)
    {
        var root = new GameObject("Dialogues").transform;
        root.SetParent(parent, false);

        DialogueVolume(root, "PitWarning", 13.2f, false, new[]
        {
            "The Ember|The dark takes the careless.",
            "The Ember|The lantern remembers where you fell.",
            "Ash|Then I'll fall lit."
        });

        DialogueVolume(root, "Midpoint", 30f, false, new[]
        {
            "Ash|Every shade I cut, I feel.",
            "The Ember|It is the grove's price. Not yours.",
            "Ash|I'll pay it. Keep the light."
        });

        DialogueVolume(root, "BossIntro", 48.2f, false, new[]
        {
            "Thorne|No further. The gate holds.",
            "Ash|Then you hold with it.",
            "Thorne|I am weary, wanderer. Still, I hold.",
            "Ash|So am I."
        });

        DialogueVolume(root, "Ending", 58.2f, true, new[]
        {
            "The Ember|Beyond lies what the grove protects.",
            "The Ember|Everyone who passes, I remember.",
            "Ash|The Dusk Gate opens to my resolve.",
            "Ash|Walk with me, Ember."
        });
    }

    static void DialogueVolume(Transform parent, string name, float x, bool gateRequired, string[] lines)
    {
        var go = new GameObject("Dialogue_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 0f, 0f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.1f, 4.2f);
        col.offset = new Vector2(0f, 2.1f);

        var trigger = go.AddComponent<DialogueTrigger>();
        trigger.lines = lines;
        trigger.once = true;
        trigger.requireGateOpen = gateRequired;
    }

    static void CreateCheckpoint(Transform parent, string name, float x)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(x, 0f, 0f);

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.4f, 2.2f);
        col.offset = new Vector2(0f, 1.1f);

        var lanternGo = new GameObject("Lantern");
        lanternGo.transform.SetParent(go.transform, false);
        lanternGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var sr = lanternGo.AddComponent<SpriteRenderer>();
        sr.sprite = art.lantern;
        sr.sortingOrder = 5;
        sr.color = new Color(2.15f, 1.55f, 0.75f, 1f);

        AddGlow("LanternGlow", lanternGo.transform, new Vector3(0f, 0.18f, 0f), 2.7f,
                new Color(1f, 0.82f, 0.5f, 1f), warmGlowMat, 4, true);
        AddBrazier(lanternGo.transform, new Vector3(0f, 0.36f, -0.8f));

        var cp = go.AddComponent<Checkpoint>();
        cp.lantern = sr;
        cp.respawnOffset = new Vector3(0f, 0.75f, 0f);
    }

    static LevelGoal CreatePortal(Transform parent)
    {
        var go = new GameObject("DuskGate");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(61.5f, 1.5f, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.portal;
        sr.sortingOrder = 6;
        sr.color = new Color(1.45f, 1.05f, 1.95f, 1f);

        AddGlow("PortalGlow", go.transform, new Vector3(0f, 0.15f, 0f), 5.4f,
                new Color(0.85f, 0.72f, 1f, 1f), violetGlowMat, 5, true);
        AddBrazier(go.transform, new Vector3(0f, 0.1f, -0.8f));

        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.5f, 2.6f);
        col.offset = new Vector2(0f, 0.05f);

        var goal = go.AddComponent<LevelGoal>();
        goal.spriteRenderer = sr;
        return goal;
    }

    // ------------------------------------------------------------ gpu atmosphere

    static void CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        warmGlowMat = MakeGlowMaterial("WarmGlow", new Color(2.5f, 1.75f, 0.8f, 1f));
        coolGlowMat = MakeGlowMaterial("CoolGlow", new Color(1.1f, 2.1f, 0.85f, 1f));
        violetGlowMat = MakeGlowMaterial("VioletGlow", new Color(1.6f, 0.9f, 2.4f, 1f));
        swingMat = MakeGlowMaterial("SwingGlow", new Color(2.2f, 1.6f, 0.9f, 1f));
        acidMat = MakeGlowMaterial("AcidGlow", new Color(1.25f, 2.3f, 0.7f, 1f));
    }

    static Material MakeGlowMaterial(string name, Color tint)
    {
        string path = "Assets/Materials/" + name + ".mat";
        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_Color", tint);
        if (art != null && art.spark != null)
            mat.SetTexture("_MainTex", art.spark.texture);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Push the PC pipeline into deliberately GPU-heavy territory: 4x MSAA,
    // 1.25x supersampling, HDR targets and full post-processing.
    static void ConfigureGpuPipeline()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (asset == null) return;

        var so = new SerializedObject(asset);
        var msaa = so.FindProperty("m_MSAA");
        if (msaa != null) msaa.intValue = 4;
        var scale = so.FindProperty("m_RenderScale");
        if (scale != null) scale.floatValue = 1.25f;
        var hdr = so.FindProperty("m_SupportsHDR");
        if (hdr != null) hdr.boolValue = true;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
    }

    static void CreatePostFx()
    {
        var go = new GameObject("PostFX");
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.sharedProfile = CreateVolumeProfile();
    }

    static VolumeProfile CreateVolumeProfile()
    {
        const string path = "Assets/Settings/HollowGroveVolume.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.value = 0.88f;
        bloom.intensity.value = 0.85f;
        bloom.scatter.value = 0.72f;
        bloom.tint.value = new Color(1f, 0.86f, 0.72f, 1f);
        bloom.highQualityFiltering.value = true;
        AssetDatabase.AddObjectToAsset(bloom, profile);

        var vignette = profile.Add<Vignette>(true);
        vignette.color.value = new Color(0.07f, 0.03f, 0.1f, 1f);
        vignette.intensity.value = 0.3f;
        vignette.smoothness.value = 0.5f;
        vignette.rounded.value = false;
        AssetDatabase.AddObjectToAsset(vignette, profile);

        var grade = profile.Add<ColorAdjustments>(true);
        grade.postExposure.value = 0.08f;
        grade.contrast.value = 7f;
        grade.saturation.value = 6f;
        AssetDatabase.AddObjectToAsset(grade, profile);

        var grain = profile.Add<FilmGrain>(true);
        grain.type.value = FilmGrainLookup.Thin1;
        grain.intensity.value = 0.09f;
        grain.response.value = 0.8f;
        AssetDatabase.AddObjectToAsset(grain, profile);

        var chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.value = 0.055f;
        AssetDatabase.AddObjectToAsset(chroma, profile);

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.value = TonemappingMode.Neutral;
        AssetDatabase.AddObjectToAsset(tone, profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    static SpriteRenderer AddGlow(string name, Transform parent, Vector3 localPos, float scale,
                                  Color tint, Material mat, int order, bool pulse)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(scale, scale, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.spark;
        sr.sharedMaterial = mat;
        sr.color = tint;
        sr.sortingOrder = order;
        if (pulse) go.AddComponent<GlowPulse>();
        return sr;
    }

    static void CreateAmbientParticles(Transform cam)
    {
        // Embers drifting up through the foreground, parented to the camera so
        // the whole level stays alive without any per-frame game logic.
        var embers = NewEmitter("Embers", cam, new Vector3(0f, -3.9f, 6.4f));
        var eMain = embers.main;
        eMain.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 9f);
        eMain.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.5f);
        eMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        eMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.5f), new Color(1f, 0.55f, 0.24f));
        eMain.gravityModifier = -0.018f;
        eMain.prewarm = true;
        eMain.maxParticles = 220;

        var eEmit = embers.emission;
        eEmit.rateOverTime = 14f;
        var eShape = embers.shape;
        eShape.shapeType = ParticleSystemShapeType.Box;
        eShape.scale = new Vector3(20f, 0.45f, 1f);

        var eVel = embers.velocityOverLifetime;
        eVel.enabled = true;
        eVel.space = ParticleSystemSimulationSpace.Local;
        eVel.x = new ParticleSystem.MinMaxCurve(-0.22f, 0.22f);
        eVel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.35f);
        eVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var eColor = embers.colorOverLifetime;
        eColor.enabled = true;
        eColor.color = new ParticleSystem.MinMaxGradient(FadeGradient(new Color(1f, 0.82f, 0.45f)));

        StyleEmitter(embers, warmGlowMat, 27);

        // Slow fireflies in the mid-ground for depth and a little colour contrast.
        var flies = NewEmitter("Fireflies", cam, new Vector3(0f, 0.3f, 6.1f));
        var fMain = flies.main;
        fMain.startLifetime = new ParticleSystem.MinMaxCurve(7f, 13f);
        fMain.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
        fMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.085f);
        fMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 1f, 0.65f), new Color(1f, 0.95f, 0.6f));
        fMain.gravityModifier = 0f;
        fMain.prewarm = true;
        fMain.maxParticles = 90;

        var fEmit = flies.emission;
        fEmit.rateOverTime = 4.5f;
        var fShape = flies.shape;
        fShape.shapeType = ParticleSystemShapeType.Box;
        fShape.scale = new Vector3(18f, 8f, 1f);

        var fNoise = flies.noise;
        fNoise.enabled = true;
        fNoise.strength = 0.5f;
        fNoise.frequency = 0.3f;
        fNoise.scrollSpeed = 0.35f;
        fNoise.quality = ParticleSystemNoiseQuality.High;

        var fColor = flies.colorOverLifetime;
        fColor.enabled = true;
        fColor.color = new ParticleSystem.MinMaxGradient(FadeGradient(new Color(0.8f, 1f, 0.55f)));

        StyleEmitter(flies, coolGlowMat, 26);
    }

    static void AddBrazier(Transform parent, Vector3 localPos)
    {
        var go = NewEmitter("Brazier", parent, localPos);
        go.transform.localPosition = localPos;

        var main = go.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.82f, 0.45f), new Color(1f, 0.5f, 0.2f));
        main.gravityModifier = -0.03f;
        main.maxParticles = 70;

        var emission = go.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(8f, 13f);

        var shape = go.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.radius = 0.12f;
        shape.angle = 14f;

        var vel = go.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        vel.y = new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var color = go.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(new Color(1f, 0.85f, 0.5f)));

        StyleEmitter(go, warmGlowMat, 13);
        go.transform.localPosition = localPos;
    }

    static ParticleSystem NewEmitter(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return ps;
    }

    static void StyleEmitter(ParticleSystem ps, Material mat, int order)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.sortingOrder = order;
        r.maxParticleSize = 0.4f;
    }

    static Gradient FadeGradient(Color c)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.14f),
                new GradientAlphaKey(0.6f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }

    // ------------------------------------------------------------ systems

    static void CreateAudio()
    {
        var go = new GameObject("Audio");
        go.AddComponent<AudioManager>();
    }

    static GameManager CreateGameManager(GameObject player)
    {
        var go = new GameObject("GameManager");
        var gm = go.AddComponent<GameManager>();
        gm.player = player.transform;
        gm.spawnPoint = new Vector3(1f, 0.75f, 0f);
        gm.bossArenaX = 49.9f;
        gm.introLines = new[]
        {
            "Ash|The dusk bleeds through the pines.",
            "The Ember|Welcome, wanderer. I remember you.",
            "The Ember|The Dusk Gate opens when the shades fall.",
            "The Ember|The grove tests the blade. Walk on.",
            "Ash|Then I walk. Dusk waits."
        };
        gm.bossDefeatLines = new[]
        {
            "Thorne|The grove takes me... at last.",
            "The Ember|He held the gate until the end.",
            "Ash|Rest, Thorne. The gate is mine."
        };
        return gm;
    }

    // ------------------------------------------------------------ ui

    static UIManager CreateUI()
    {
        var canvasGo = new GameObject("UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var ui = canvasGo.AddComponent<UIManager>();

        var heartsRoot = new GameObject("Hearts", typeof(RectTransform));
        heartsRoot.transform.SetParent(canvasGo.transform, false);
        Stretch(heartsRoot.GetComponent<RectTransform>());

        var heartImages = new List<Image>();
        for (int i = 0; i < 3; i++)
        {
            var img = NewImage("Heart_" + (i + 1), heartsRoot.transform, Color.white, false);
            img.sprite = art.heart;
            img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(26f + i * 48f, -24f);
            rt.sizeDelta = new Vector2(40f, 36f);
            heartImages.Add(img);
        }

        var title = NewText("ZoneLabel", canvasGo.transform, "HOLLOW GROVE    ·    I",
            18, new Color(0.72f, 0.66f, 0.79f, 0.8f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -26f), new Vector2(700f, 40f));

        var objective = NewText("Objective", canvasGo.transform, "SHADES   0 / 8",
            20, new Color(0.72f, 0.66f, 0.79f, 0.9f), TextAnchor.MiddleRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-28f, -26f), new Vector2(520f, 40f));

        var message = NewText("Message", canvasGo.transform, "",
            38, new Color(0.96f, 0.96f, 0.86f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 110f), new Vector2(1400f, 160f));

        var prompt = NewText("Prompt", canvasGo.transform, "",
            22, new Color(0.72f, 0.66f, 0.79f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 46f), new Vector2(1500f, 44f));

        // Boss bar: name above a red fill, top-centre under the zone label.
        var bossRoot = new GameObject("BossBar", typeof(RectTransform));
        bossRoot.transform.SetParent(canvasGo.transform, false);
        var bossRt = bossRoot.GetComponent<RectTransform>();
        bossRt.anchorMin = new Vector2(0.5f, 1f);
        bossRt.anchorMax = new Vector2(0.5f, 1f);
        bossRt.pivot = new Vector2(0.5f, 1f);
        bossRt.anchoredPosition = new Vector2(0f, -88f);
        bossRt.sizeDelta = new Vector2(780f, 70f);
        var bossBg = bossRoot.AddComponent<Image>();
        bossBg.color = new Color(0.035f, 0.02f, 0.055f, 0.8f);
        bossBg.raycastTarget = false;

        var bossName = NewText("BossName", bossRoot.transform, "THORNE",
            21, new Color(0.94f, 0.82f, 0.94f, 1f), TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -3f), new Vector2(-32f, 30f));

        var bossFillGo = new GameObject("BossFill", typeof(RectTransform));
        bossFillGo.transform.SetParent(bossRoot.transform, false);
        var bossFillRt = bossFillGo.GetComponent<RectTransform>();
        bossFillRt.anchorMin = new Vector2(0f, 0f);
        bossFillRt.anchorMax = new Vector2(1f, 0f);
        bossFillRt.pivot = new Vector2(0.5f, 0f);
        bossFillRt.anchoredPosition = new Vector2(0f, 10f);
        bossFillRt.sizeDelta = new Vector2(-24f, 14f);
        var bossFill = bossFillGo.AddComponent<Image>();
        bossFill.color = new Color(0.86f, 0.32f, 0.32f, 1f);
        bossFill.raycastTarget = false;
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        bossFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        bossFill.fillAmount = 1f;

        bossRoot.SetActive(false);

        // Letterboxed conversation panel, driven by DialogueSystem.
        var panel = new GameObject("DialoguePanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(0.5f, 0f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(0f, 220f);
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.03f, 0.02f, 0.05f, 0.88f);
        panelBg.raycastTarget = false;

        var speakerText = NewText("Speaker", panel.transform, "",
            24, new Color(0.92f, 0.66f, 0.30f, 1f), TextAnchor.MiddleLeft,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(38f, -16f), new Vector2(800f, 36f));

        var bodyText = NewText("Body", panel.transform, "",
            30, new Color(0.96f, 0.96f, 0.86f, 1f), TextAnchor.UpperLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(-76f, -96f));

        var hintText = NewText("Hint", panel.transform, "",
            18, new Color(0.72f, 0.66f, 0.79f, 1f), TextAnchor.MiddleRight,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-38f, 14f), new Vector2(800f, 30f));

        var dialogue = canvasGo.AddComponent<DialogueSystem>();
        dialogue.panel = panel;
        dialogue.speakerText = speakerText;
        dialogue.bodyText = bodyText;
        dialogue.hintText = hintText;
        panel.SetActive(false);

        // Damage flash sits above the HUD but below the fade-to-black layer.
        var flash = NewImage("DamageFlash", canvasGo.transform, new Color(0.62f, 0.04f, 0.06f, 0f), false);
        Stretch(flash.rectTransform);

        var fade = NewImage("Fade", canvasGo.transform, new Color(0f, 0f, 0f, 1f), false);
        Stretch(fade.rectTransform);
        fade.transform.SetAsLastSibling();

        // Messages and the restart prompt must stay readable on the death
        // veil, so they sit above the fade layer.
        message.transform.SetAsLastSibling();
        prompt.transform.SetAsLastSibling();

        ui.hearts = heartImages.ToArray();
        ui.messageText = message;
        ui.promptText = prompt;
        ui.objectiveText = objective;
        ui.fadeImage = fade;
        ui.bossRoot = bossRoot;
        ui.bossNameText = bossName;
        ui.bossFillImage = bossFill;
        ui.damageFlash = flash;
        if (title != null) title.raycastTarget = false;

        return ui;
    }

    static Font UiFont()
    {
        if (uiFont != null) return uiFont;
        uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (uiFont == null) uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 20);
        return uiFont;
    }

    static Image NewImage(string name, Transform parent, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    static Text NewText(string name, Transform parent, string content, int size, Color color,
                        TextAnchor textAnchor, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                        Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var t = go.AddComponent<Text>();
        t.font = UiFont();
        t.fontSize = size;
        t.color = color;
        t.text = content;
        t.alignment = textAnchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return t;
    }
}
