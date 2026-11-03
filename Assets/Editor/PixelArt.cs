using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class SpriteBank
{
    public Sprite hero;
    public Sprite slime;
    public Sprite brute;
    public Sprite ground;
    public Sprite platform;
    public Sprite sky;
    public Sprite moon;
    public Sprite hills;
    public Sprite trees;
    public Sprite foliage;
    public Sprite portal;
    public Sprite swing;
    public Sprite lantern;
    public Sprite heart;
    public Sprite soil;
    public Sprite stars;
    public Sprite spark;
    public Sprite stalker;
    public Sprite spitter;
    public Sprite acid;
    public Sprite wave;
    public Sprite grass;
    public Sprite mist;
    public Sprite arch;
}

// Generates every sprite in Hollow Grove procedurally and imports it as
// point-filtered pixel art at 32 pixels per unit.
public static class PixelArt
{
    const string Folder = "Assets/Art/Generated";

    public static readonly Color32 Ink = new Color32(0x0E, 0x0B, 0x16, 255);
    public static readonly Color32 Cloak = new Color32(0x24, 0x1F, 0x38, 255);
    public static readonly Color32 CloakDark = new Color32(0x1A, 0x16, 0x28, 255);
    public static readonly Color32 CloakLight = new Color32(0x3A, 0x31, 0x50, 255);
    public static readonly Color32 Ember = new Color32(0xE0, 0x9A, 0x3B, 255);
    public static readonly Color32 EmberPale = new Color32(0xF5, 0xD9, 0x8A, 255);
    public static readonly Color32 Ivory = new Color32(0xF5, 0xF5, 0xDC, 255);
    public static readonly Color32 Lavender = new Color32(0xB8, 0xA9, 0xC9, 255);
    public static readonly Color32 Skin = new Color32(0xE8, 0xC4, 0xA0, 255);
    public static readonly Color32 SkinShade = new Color32(0xC9, 0xA1, 0x84, 255);
    public static readonly Color32 SlimeBody = new Color32(0x7F, 0xB0, 0x69, 255);
    public static readonly Color32 SlimeDark = new Color32(0x4E, 0x7A, 0x46, 255);
    public static readonly Color32 SlimeLight = new Color32(0xA8, 0xD0, 0x8D, 255);
    public static readonly Color32 Stone = new Color32(0x3A, 0x33, 0x48, 255);
    public static readonly Color32 StoneLight = new Color32(0x5E, 0x50, 0x76, 255);
    public static readonly Color32 StoneDark = new Color32(0x24, 0x1E, 0x2E, 255);
    public static readonly Color32 Moss = new Color32(0x5B, 0x6E, 0x45, 255);
    public static readonly Color32 MossLight = new Color32(0x9C, 0xBF, 0x6C, 255);

    static readonly string[] Names =
    {
        "hero", "slime", "brute", "ground", "platform", "sky", "moon",
        "hills", "trees", "foliage", "portal", "swing", "lantern", "heart",
        "soil", "stars", "spark", "stalker", "spitter", "acid", "wave",
        "grass", "mist", "arch"
    };

    static readonly System.Random rng = new System.Random(1337);

    public static SpriteBank Generate()
    {
        string abs = Path.Combine(Application.dataPath, "Art/Generated");
        Directory.CreateDirectory(abs);

        Save(abs, "hero", DrawHero());
        Save(abs, "slime", DrawSlime());
        Save(abs, "brute", DrawBrute());
        Save(abs, "ground", DrawGround());
        Save(abs, "platform", DrawPlatform());
        Save(abs, "sky", DrawSky());
        Save(abs, "moon", DrawMoon());
        Save(abs, "hills", DrawHills());
        Save(abs, "trees", DrawTrees());
        Save(abs, "foliage", DrawFoliage());
        Save(abs, "portal", DrawPortal());
        Save(abs, "swing", DrawSwing());
        Save(abs, "lantern", DrawLantern());
        Save(abs, "heart", DrawHeart());
        Save(abs, "soil", DrawSoil());
        Save(abs, "stars", DrawStars());
        Save(abs, "spark", DrawSpark());
        Save(abs, "stalker", DrawStalker());
        Save(abs, "spitter", DrawSpitter());
        Save(abs, "acid", DrawAcid());
        Save(abs, "wave", DrawWave());
        Save(abs, "grass", DrawGrass());
        Save(abs, "mist", DrawMist());
        Save(abs, "arch", DrawArch());

        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        foreach (var name in Names) Configure(name);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

        return new SpriteBank
        {
            hero = Load("hero"),
            slime = Load("slime"),
            brute = Load("brute"),
            ground = Load("ground"),
            platform = Load("platform"),
            sky = Load("sky"),
            moon = Load("moon"),
            hills = Load("hills"),
            trees = Load("trees"),
            foliage = Load("foliage"),
            portal = Load("portal"),
            swing = Load("swing"),
            lantern = Load("lantern"),
            heart = Load("heart"),
            soil = Load("soil"),
            stars = Load("stars"),
            spark = Load("spark"),
            stalker = Load("stalker"),
            spitter = Load("spitter"),
            acid = Load("acid"),
            wave = Load("wave"),
            grass = Load("grass"),
            mist = Load("mist"),
            arch = Load("arch")
        };
    }

    static void Save(string absFolder, string name, PixelCanvas c)
    {
        var tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false);
        tex.SetPixels32(c.Pixels);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(absFolder, name + ".png"), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    static void Configure(string name)
    {
        string path = Folder + "/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new Exception("no importer for " + path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.npotScale = TextureImporterNPOTScale.None;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteExtrude = 0;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static Sprite Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
    }

    // ------------------------------------------------------------ characters

    static PixelCanvas DrawHero()
    {
        var c = new PixelCanvas(32, 48);
        c.FillEllipse(16, 20, 10, 15, CloakDark);

        // legs and boots
        c.Rect(11, 4, 5, 12, CloakDark);
        c.Rect(17, 4, 5, 12, CloakDark);
        c.Rect(10, 0, 6, 5, Ink);
        c.Rect(17, 0, 6, 5, Ink);

        // tunic + flared skirt
        c.Rect(10, 16, 13, 17, Cloak);
        c.FillEllipse(16, 15, 9, 8, Cloak);
        c.FillTriangle(8, 8, 24, 8, 16, 21, Cloak);
        c.FillTriangle(9, 8, 23, 8, 16, 17, CloakDark);

        // belt
        c.Rect(10, 17, 13, 2, new Color32(0x6B, 0x4A, 0x2E, 255));
        c.Rect(15, 17, 3, 2, Ember);

        // shoulders and arms
        c.Rect(8, 25, 5, 7, CloakLight);
        c.Rect(20, 25, 5, 7, CloakLight);
        c.Rect(8, 21, 4, 6, Cloak);
        c.Rect(21, 21, 4, 6, Cloak);
        c.Rect(22, 19, 3, 3, Skin);

        // ember scarf
        c.Rect(9, 30, 14, 3, Ember);
        c.Rect(7, 27, 4, 4, Ember);
        c.Set(6, 26, EmberPale);

        // hood and face
        c.FillEllipse(16, 37, 8, 9, Cloak);
        c.Rect(12, 34, 9, 7, Skin);
        c.Rect(12, 34, 9, 2, SkinShade);
        c.Rect(13, 37, 2, 2, Ink);
        c.Rect(18, 37, 2, 2, Ink);
        c.Rect(11, 39, 11, 4, Cloak);
        c.Rect(11, 42, 11, 3, CloakDark);
        c.Set(16, 44, CloakLight);

        // sword
        c.Line(24, 22, 28, 8, 2, Ivory);
        c.Line(25, 22, 29, 8, 1, new Color32(0xD8, 0xD4, 0xBE, 255));
        c.Rect(21, 21, 7, 2, new Color32(0xC0, 0x8A, 0x3A, 255));
        c.Rect(23, 22, 2, 3, new Color32(0x4A, 0x30, 0x18, 255));
        c.Set(28, 7, EmberPale);

        c.Outline(Ink);
        c.RimSide(1, new Color32(0xF5, 0xD9, 0x8A, 190));
        c.Set(11, 39, Lavender);
        c.Set(10, 30, EmberPale);
        c.Set(28, 7, new Color32(0xFF, 0xF3, 0xD0, 255));
        return c;
    }

    static PixelCanvas DrawSlime()
    {
        var c = new PixelCanvas(24, 18);
        c.FillEllipse(12, 8, 10, 7, SlimeBody);
        c.FillEllipse(12, 5, 9, 4, SlimeDark);
        c.FillEllipse(12, 9, 10, 6, SlimeBody);
        c.FillEllipse(8, 11, 3, 2, SlimeLight);
        c.Rect(8, 8, 2, 3, Ink);
        c.Rect(15, 8, 2, 3, Ink);
        c.Rect(11, 6, 3, 1, SlimeDark);
        c.Set(7, 12, new Color32(0xC8, 0xE8, 0xAC, 255));
        c.Outline(new Color32(0x2C, 0x3B, 0x28, 255));
        return c;
    }

    static PixelCanvas DrawBrute()
    {
        var c = new PixelCanvas(44, 44);
        Color32 body = new Color32(0x3A, 0x2E, 0x4A, 255);
        Color32 bodyLight = new Color32(0x54, 0x42, 0x68, 255);
        Color32 bodyDark = new Color32(0x24, 0x1C, 0x32, 255);

        c.Rect(10, 0, 8, 9, bodyDark);
        c.Rect(26, 0, 8, 9, bodyDark);
        c.Rect(9, 0, 10, 3, Ink);
        c.Rect(25, 0, 10, 3, Ink);

        c.FillEllipse(22, 20, 16, 14, body);
        c.Rect(8, 10, 28, 12, body);
        c.FillEllipse(22, 25, 14, 8, bodyLight);
        c.FillEllipse(22, 14, 12, 6, bodyDark);

        c.FillEllipse(6, 20, 6, 10, body);
        c.FillEllipse(38, 20, 6, 10, body);
        c.Line(3, 12, 1, 7, 2, bodyDark);
        c.Line(7, 11, 6, 6, 2, bodyDark);
        c.Line(41, 12, 43, 7, 2, bodyDark);
        c.Line(37, 11, 38, 6, 2, bodyDark);

        c.FillEllipse(22, 34, 11, 9, bodyLight);
        c.Line(13, 39, 8, 43, 3, new Color32(0xD8, 0xD4, 0xBE, 255));
        c.Line(31, 39, 36, 43, 3, new Color32(0xD8, 0xD4, 0xBE, 255));
        c.Rect(16, 34, 4, 3, Ember);
        c.Rect(25, 34, 4, 3, Ember);
        c.Set(17, 36, EmberPale);
        c.Set(26, 36, EmberPale);
        c.Rect(19, 30, 7, 2, Ink);

        c.Rect(20, 18, 4, 6, Ember);
        c.Rect(21, 20, 2, 2, EmberPale);
        c.Line(14, 12, 18, 18, 1, bodyDark);
        c.Line(30, 22, 25, 16, 1, bodyDark);

        c.Outline(Ink);
        return c;
    }

    // ------------------------------------------------------------ environment

    static PixelCanvas DrawGround()
    {
        var c = new PixelCanvas(32, 32);
        c.Rect(0, 0, 32, 32, new Color32(0x24, 0x1E, 0x2E, 255));
        c.Rect(0, 0, 32, 26, new Color32(0x2A, 0x24, 0x33, 255));

        for (int i = 0; i < 30; i++)
        {
            int x = rng.Next(0, 30);
            int y = rng.Next(1, 24);
            c.Rect(x, y, rng.Next(1, 4), rng.Next(1, 3), new Color32(0x34, 0x2D, 0x42, 255));
        }
        for (int i = 0; i < 5; i++)
        {
            int x = rng.Next(2, 29);
            c.Line(x, rng.Next(2, 22), x + rng.Next(-4, 5), rng.Next(2, 22), 1, new Color32(0x1B, 0x16, 0x24, 255));
        }

        c.Rect(0, 26, 32, 2, new Color32(0x46, 0x54, 0x36, 255));
        c.Rect(0, 28, 32, 3, Moss);
        c.Rect(0, 31, 32, 1, new Color32(0xA8, 0xC8, 0x72, 255));
        for (int x = 0; x < 32; x += 2)
            if (rng.NextDouble() < 0.6)
                c.Rect(x, 30, 1, rng.Next(1, 2), MossLight);
        for (int x = 1; x < 32; x += 9)
            c.Set(x, 31, EmberPale);
        return c;
    }

    static PixelCanvas DrawPlatform()
    {
        var c = new PixelCanvas(32, 16);
        c.Rect(0, 0, 32, 16, new Color32(0x19, 0x15, 0x22, 255));
        c.Rect(0, 3, 32, 11, Stone);
        c.Rect(0, 12, 32, 3, StoneLight);
        c.Rect(0, 13, 32, 2, Moss);
        for (int x = 2; x < 32; x += 7)
            c.Rect(x, 10, 3, 1, StoneDark);
        for (int i = 0; i < 5; i++)
            c.Rect(rng.Next(0, 30), 1, 2, rng.Next(1, 3), Moss);
        return c;
    }

    static PixelCanvas DrawSky()
    {
        var c = new PixelCanvas(4, 256);
        float[] stops = { 0f, 0.16f, 0.32f, 0.43f, 0.51f, 0.57f, 0.62f, 0.67f, 0.74f, 0.83f, 0.92f, 1f };
        Color32[] cols =
        {
            new Color32(0x1A, 0x16, 0x26, 255),
            new Color32(0x24, 0x1C, 0x32, 255),
            new Color32(0x3A, 0x2F, 0x52, 255),
            new Color32(0x6B, 0x3F, 0x5B, 255),
            new Color32(0xA9, 0x55, 0x4F, 255),
            new Color32(0xD9, 0x8A, 0x45, 255),
            new Color32(0xE0, 0x9A, 0x3B, 255),
            new Color32(0xC8, 0x76, 0x4A, 255),
            new Color32(0x8A, 0x4A, 0x5A, 255),
            new Color32(0x4A, 0x2F, 0x4E, 255),
            new Color32(0x2B, 0x23, 0x45, 255),
            new Color32(0x15, 0x12, 0x2A, 255)
        };

        for (int y = 0; y < 256; y++)
        {
            float t = y / 255f;
            Color32 col = Gradient(t, stops, cols);
            for (int x = 0; x < 4; x++)
            {
                int j = rng.Next(-2, 3);
                c.Set(x, y, new Color32(
                    (byte)Mathf.Clamp(col.r + j, 0, 255),
                    (byte)Mathf.Clamp(col.g + j, 0, 255),
                    (byte)Mathf.Clamp(col.b + j, 0, 255), 255));
            }
        }
        return c;
    }

    static PixelCanvas DrawMoon()
    {
        var c = new PixelCanvas(96, 96);

        // layered glow, building a soft halo around the disk
        for (int i = 0; i < 11; i++)
        {
            int r = 46 - i * 2;
            byte a = (byte)(7 + i * 3);
            c.FillEllipse(48, 48, r, r, new Color32(0xFF, 0xE8, 0xC0, a));
        }

        c.FillEllipse(48, 48, 24, 24, new Color32(0xEE, 0xE8, 0xD3, 255));
        c.FillEllipse(45, 46, 21, 21, new Color32(0xF7, 0xF3, 0xE3, 255));
        c.FillEllipse(40, 56, 5, 5, new Color32(0xD9, 0xD3, 0xBE, 190));
        c.FillEllipse(56, 59, 4, 4, new Color32(0xD9, 0xD3, 0xBE, 165));
        c.FillEllipse(55, 40, 3, 3, new Color32(0xD9, 0xD3, 0xBE, 140));
        c.FillEllipse(43, 34, 2, 2, new Color32(0xD9, 0xD3, 0xBE, 120));
        c.FillEllipse(36, 46, 2, 2, new Color32(0xFF, 0xFF, 0xF2, 255));
        c.Set(48, 74, new Color32(0xFF, 0xFF, 0xF2, 235));
        return c;
    }

    static PixelCanvas DrawStars()
    {
        var c = new PixelCanvas(320, 180);
        for (int i = 0; i < 150; i++)
        {
            int x = rng.Next(0, 320);
            int y = rng.Next(24, 180);
            byte a = (byte)(60 + rng.Next(0, 170));
            int size = rng.NextDouble() < 0.1 ? 2 : 1;
            var col = rng.NextDouble() < 0.3
                ? new Color32(0xC9, 0xB8, 0xE8, a)
                : new Color32(0xFF, 0xF3, 0xD6, a);
            c.Rect(x, y, size, size, col);
        }
        c.Rect(240, 140, 2, 2, new Color32(0xFF, 0xFF, 0xF2, 255));
        c.Rect(60, 108, 1, 1, new Color32(0xFF, 0xFF, 0xF2, 255));
        return c;
    }

    static PixelCanvas DrawSpark()
    {
        // Soft radial dot used by halos and the GPU particle emitters.
        var c = new PixelCanvas(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dx = (x - 15.5f) / 15.5f;
                float dy = (y - 15.5f) / 15.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                float a = Mathf.Pow(1f - d, 2.1f);
                c.Set(x, y, new Color32(0xFF, 0xF6, 0xE0, (byte)(a * 255f)));
            }
        }
        return c;
    }

    static PixelCanvas DrawStalker()
    {
        // A low, shadowy ambusher: four legs, a raised hackle, two ember eyes.
        var c = new PixelCanvas(40, 28);
        Color32 fur = new Color32(0x21, 0x1B, 0x33, 255);
        Color32 furLight = new Color32(0x39, 0x2F, 0x53, 255);
        Color32 furDark = new Color32(0x14, 0x10, 0x22, 255);

        c.FillEllipse(17, 11, 14, 7, fur);
        c.FillEllipse(13, 16, 9, 8, fur);
        c.FillEllipse(29, 15, 7, 7, fur);
        c.FillEllipse(12, 18, 6, 5, furLight);

        // hackles
        c.FillTriangle(6, 21, 10, 27, 14, 20, furDark);
        c.FillTriangle(12, 22, 16, 28, 20, 21, furDark);
        c.FillTriangle(18, 21, 22, 26, 25, 20, furDark);

        // legs
        c.Rect(8, 0, 3, 9, furDark);
        c.Rect(15, 0, 3, 8, furDark);
        c.Rect(23, 0, 3, 8, furDark);
        c.Rect(31, 0, 3, 9, furDark);
        c.Rect(7, 0, 5, 2, Ink);
        c.Rect(14, 0, 5, 2, Ink);
        c.Rect(22, 0, 5, 2, Ink);
        c.Rect(30, 0, 5, 2, Ink);

        // tail
        c.Line(4, 12, 0, 19, 2, furDark);
        c.Line(4, 13, 1, 22, 1, fur);

        // head, ears, eyes
        c.FillTriangle(30, 21, 33, 27, 35, 20, furDark);
        c.FillTriangle(24, 21, 26, 26, 28, 20, fur);
        c.Rect(30, 15, 3, 3, Ember);
        c.Set(31, 16, EmberPale);
        c.Rect(25, 15, 2, 2, EmberPale);
        c.Rect(33, 13, 3, 1, furDark);

        c.Outline(Ink);
        c.RimTop(new Color32(0x5A, 0x4A, 0x78, 190), 4);
        return c;
    }

    static PixelCanvas DrawSpitter()
    {
        // A rooted bile-spitter: fat sac, dripping mouth, nervous little eyes.
        var c = new PixelCanvas(32, 34);
        Color32 flesh = new Color32(0x55, 0x6B, 0x4B, 255);
        Color32 fleshLight = new Color32(0x86, 0xA8, 0x6B, 255);
        Color32 bile = new Color32(0x9C, 0xD6, 0x6A, 255);
        Color32 bileDark = new Color32(0x4F, 0x7A, 0x3A, 255);

        c.Rect(8, 0, 3, 7, bileDark);
        c.Rect(14, 0, 3, 6, bileDark);
        c.Rect(20, 0, 3, 7, bileDark);
        c.Rect(7, 0, 5, 2, Ink);
        c.Rect(13, 0, 5, 2, Ink);
        c.Rect(19, 0, 5, 2, Ink);

        c.FillEllipse(16, 14, 12, 12, flesh);
        c.FillEllipse(16, 20, 9, 8, fleshLight);
        c.FillEllipse(16, 9, 10, 7, bileDark);
        c.FillEllipse(16, 8, 8, 5, bile);

        c.Rect(12, 20, 2, 3, Ink);
        c.Rect(20, 20, 2, 3, Ink);
        c.Rect(13, 21, 1, 1, EmberPale);
        c.Rect(21, 21, 1, 1, EmberPale);

        // dripping mouth
        c.Rect(14, 15, 5, 2, Ink);
        c.Rect(15, 13, 3, 2, bile);
        c.Set(16, 12, new Color32(0xDF, 0xF6, 0xA8, 255));
        c.Line(12, 16, 11, 12, 1, bile);

        // thorny tendrils
        c.Line(5, 10, 1, 6, 2, bileDark);
        c.Line(27, 10, 31, 6, 2, bileDark);
        c.Line(6, 18, 2, 20, 1, flesh);
        c.Line(26, 18, 30, 20, 1, flesh);

        c.Outline(new Color32(0x22, 0x30, 0x22, 255));
        c.RimTop(new Color32(0xB8, 0xDC, 0x8A, 140), 3);
        return c;
    }

    static PixelCanvas DrawAcid()
    {
        var c = new PixelCanvas(14, 14);
        for (int y = 0; y < 14; y++)
        {
            for (int x = 0; x < 14; x++)
            {
                float dx = (x - 6.5f) / 6f;
                float dy = (y - 6.5f) / 6f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                byte a = (byte)(d > 0.78f ? 60 : 235);
                Color32 col = Color32.Lerp(new Color32(0x3F, 0x6B, 0x35, a),
                                           new Color32(0xCF, 0xF5, 0x8E, a), 1f - d);
                c.Set(x, y, col);
            }
        }
        c.Set(5, 9, new Color32(0xF2, 0xFF, 0xC8, 255));
        c.Set(8, 5, new Color32(0x8F, 0xC9, 0x5E, 255));
        return c;
    }

    static PixelCanvas DrawWave()
    {
        // A ground-hugging crescent of dust and splintered stone.
        var c = new PixelCanvas(72, 22);
        for (int x = 0; x < 72; x++)
        {
            float k = x / 71f;
            float h = Mathf.Sin(k * Mathf.PI) * 16f;
            for (int y = 0; y < (int)h; y++)
            {
                float edge = Mathf.Abs(y - h * 0.35f) / Mathf.Max(1f, h);
                byte a = (byte)(Mathf.Clamp01(1f - edge) * 190f);
                Color32 col = y > h * 0.6f
                    ? new Color32(0x5E, 0x50, 0x76, a)
                    : new Color32(0x9C, 0xBF, 0x6C, (byte)(a * 0.6f));
                c.Set(x, y, col);
            }
        }
        for (int i = 0; i < 14; i++)
            c.Set(8 + rng.Next(0, 56), 2 + rng.Next(0, 12), new Color32(0xE0, 0x9A, 0x3B, 160));
        return c;
    }

    static PixelCanvas DrawGrass()
    {
        var c = new PixelCanvas(512, 74);
        Color32 dark = new Color32(0x0B, 0x09, 0x12, 255);
        Color32 mid = new Color32(0x1A, 0x15, 0x27, 255);
        Color32 tip = new Color32(0x4A, 0x3F, 0x60, 255);

        c.Rect(0, 0, 512, 7, dark);
        for (int i = 0; i < 190; i++)
        {
            int x = rng.Next(0, 512);
            int h = 14 + rng.Next(0, 52);
            int lean = rng.Next(-7, 8);
            Color32 col = rng.Next(0, 3) == 0 ? mid : dark;
            c.Line(x, 4, x + lean, h, 1 + rng.Next(0, 2), col);
            if (rng.Next(0, 9) == 0) c.Set(x + lean, h, tip);
        }
        return c;
    }

    static PixelCanvas DrawMist()
    {
        var c = new PixelCanvas(256, 96);
        Color32 mistCol = new Color32(0x8C, 0x7E, 0xB8, 255);
        for (int y = 0; y < 96; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                float dx = (x - 128f) / 126f;
                float dy = (y - 46f) / 42f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                float a = Mathf.Pow(1f - d, 2.2f) * 42f;
                c.Set(x, y, new Color32(mistCol.r, mistCol.g, mistCol.b, (byte)a));
            }
        }
        return c;
    }

    static PixelCanvas DrawArch()
    {
        var c = new PixelCanvas(128, 120);
        c.Rect(10, 0, 18, 74, Stone);
        c.Rect(100, 0, 18, 74, Stone);
        c.Rect(6, 0, 26, 8, StoneDark);
        c.Rect(96, 0, 26, 8, StoneDark);

        // Half-ring arch, drawn as a thick arc so the opening stays transparent.
        for (int a = 0; a <= 180; a++)
        {
            float rad = a * Mathf.Deg2Rad;
            for (int t = 0; t < 13; t++)
            {
                int x = 64 + Mathf.RoundToInt(Mathf.Cos(rad) * (52 - t));
                int y = 72 + Mathf.RoundToInt(Mathf.Sin(rad) * (52 - t));
                c.Set(x, y, t < 3 ? StoneLight : Stone);
            }
        }
        c.Rect(28, 64, 72, 6, Stone);
        c.Rect(24, 58, 80, 6, StoneDark);

        // broken crown
        c.Rect(40, 108, 10, 12, Stone);
        c.Rect(58, 112, 9, 8, StoneLight);
        c.Rect(74, 106, 12, 14, Stone);
        c.FillTriangle(30, 42, 40, 58, 44, 40, StoneLight);
        c.FillTriangle(98, 46, 88, 60, 84, 42, StoneDark);

        // moss and embers
        for (int i = 0; i < 26; i++)
            c.Set(14 + rng.Next(0, 100), 6 + rng.Next(0, 70),
                  new Color32(0x5B, 0x6E, 0x45, (byte)(120 + rng.Next(0, 90))));
        c.Rect(60, 44, 8, 3, Ember);
        c.Set(62, 45, EmberPale);
        c.Set(66, 45, EmberPale);

        c.Outline(Ink);
        return c;
    }

    static PixelCanvas DrawSoil()
    {
        // 32x96 = exactly one 1x3 unit tile, so tiled ground has no vertical seam.
        var c = new PixelCanvas(32, 96);
        Color32 top = new Color32(0x24, 0x1E, 0x2E, 255);
        Color32 bottom = new Color32(0x12, 0x0F, 0x1E, 255);

        for (int y = 0; y < 96; y++)
        {
            float t = y / 95f;
            Color32 col = Color32.Lerp(bottom, top, t);
            c.Rect(0, y, 32, 1, col);
        }

        for (int i = 0; i < 26; i++)
        {
            int x = rng.Next(0, 30);
            int y = rng.Next(2, 92);
            int w = rng.Next(1, 5);
            int h = rng.Next(1, 3);
            Color32 rock = rng.NextDouble() < 0.5
                ? new Color32(0x2E, 0x27, 0x3C, 255)
                : new Color32(0x1A, 0x15, 0x26, 255);
            c.Rect(x, y, w, h, rock);
        }

        c.Rect(0, 92, 32, 4, new Color32(0x2A, 0x24, 0x33, 255));
        return c;
    }

    static PixelCanvas DrawHills()
    {
        var c = new PixelCanvas(512, 160);
        Color32 hill = new Color32(0x3A, 0x34, 0x62, 255);
        c.FillEllipse(60, 40, 120, 90, hill);
        c.FillEllipse(210, 30, 150, 100, hill);
        c.FillEllipse(380, 45, 140, 95, hill);
        c.FillEllipse(500, 25, 100, 80, hill);
        c.Rect(0, 0, 512, 25, hill);
        c.RimTop(new Color32(0x6E, 0x60, 0x96, 255), 190);
        return c;
    }

    static PixelCanvas DrawTrees()
    {
        var c = new PixelCanvas(512, 256);
        Color32 trunk = new Color32(0x1D, 0x19, 0x30, 255);
        Color32 canopy = new Color32(0x26, 0x20, 0x40, 255);
        int[] xs = { 28, 86, 148, 210, 268, 330, 394, 452, 500 };

        foreach (int x in xs)
        {
            int h = 80 + rng.Next(0, 80);
            c.Rect(x - 3, 0, 6, h, trunk);
            c.Line(x, 10, x - 12, 0, 2, trunk);
            c.Line(x, 10, x + 12, 0, 2, trunk);
            c.Line(x, (int)(h * 0.55f), x - 20, (int)(h * 0.75f), 2, trunk);
            c.Line(x, (int)(h * 0.7f), x + 22, (int)(h * 0.85f), 2, trunk);
            c.FillEllipse(x, h + 16, 34, 24, canopy);
            c.FillEllipse(x - 22, h + 4, 22, 18, canopy);
            c.FillEllipse(x + 22, h + 8, 24, 20, canopy);
            c.FillEllipse(x, h + 34, 24, 16, canopy);
        }

        c.RimTop(new Color32(0x4C, 0x42, 0x72, 255), 165);
        return c;
    }

    static PixelCanvas DrawFoliage()
    {
        var c = new PixelCanvas(512, 128);
        Color32 bush = new Color32(0x14, 0x11, 0x1F, 255);
        Color32 bushLight = new Color32(0x33, 0x2B, 0x4C, 255);
        int[] xs = { 35, 120, 215, 310, 400, 480 };

        foreach (int x in xs)
        {
            int w = 50 + rng.Next(0, 25);
            int h = 22 + rng.Next(0, 18);
            c.FillEllipse(x, 10, w, h, bush);
            c.FillEllipse(x - w / 2 + 8, 8, 20, 14, bush);
            c.FillEllipse(x + w / 2 - 6, 9, 18, 12, bush);
            for (int b = 0; b < 10; b++)
            {
                int gx = x - w / 2 + rng.Next(0, w);
                c.Line(gx, 0, gx + rng.Next(-4, 5), rng.Next(6, 20), 1, bushLight);
            }
        }

        for (int i = 0; i < 16; i++)
            c.Set(rng.Next(0, 512), rng.Next(18, 118),
                  new Color32(0xE0, 0x9A, 0x3B, (byte)(100 + rng.Next(0, 100))));

        c.RimTop(new Color32(0x42, 0x3A, 0x5E, 255), 90);
        return c;
    }

    // ------------------------------------------------------------ props

    static PixelCanvas DrawPortal()
    {
        var c = new PixelCanvas(64, 96);
        Color32 deep = new Color32(0x2B, 0x1E, 0x4A, 255);
        Color32 violet = new Color32(0x6B, 0x4A, 0x9B, 255);
        Color32 bright = new Color32(0xB8, 0xA9, 0xC9, 255);

        c.Rect(4, 0, 56, 7, StoneDark);
        c.Rect(6, 3, 52, 4, Stone);

        c.FillEllipse(32, 52, 30, 38, new Color32(0xE0, 0x9A, 0x3B, 26));
        c.FillEllipse(32, 52, 26, 34, new Color32(0xB8, 0xA9, 0xC9, 30));

        for (int y = 20; y < 88; y++)
        {
            for (int x = 4; x < 60; x++)
            {
                float dx = (x - 32) / 21f;
                float dy = (y - 52) / 30f;
                if (dx * dx + dy * dy > 1f) continue;

                float t = Mathf.InverseLerp(20f, 82f, y);
                Color32 col = Color32.Lerp(deep, violet, t);
                float swirl = Mathf.Sin(x * 0.35f + y * 0.22f);
                if (swirl > 0.92f) col = bright;
                else if (swirl > 0.8f) col = Color32.Lerp(col, bright, 0.4f);
                c.Set(x, y, col);
            }
        }

        for (int i = 0; i < 18; i++)
            c.Set(32 + (int)(Mathf.Sin(i * 0.7f) * 12), 24 + i * 3,
                  new Color32(0xF5, 0xD9, 0x8A, 160));

        c.RingEllipse(32, 52, 27, 36, 8, Stone);
        c.RingEllipse(32, 52, 27, 36, 2, StoneLight);
        c.Rect(5, 4, 9, 50, Stone);
        c.Rect(50, 4, 9, 50, Stone);
        c.Rect(5, 46, 9, 4, StoneLight);
        c.Rect(50, 46, 9, 4, StoneLight);

        c.Outline(Ink);
        return c;
    }

    static PixelCanvas DrawSwing()
    {
        var c = new PixelCanvas(48, 48);
        float cx = 8f, cy = 24f;
        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 15f || d > 27f) continue;
                float ang = Mathf.Atan2(dy, dx);
                if (Mathf.Abs(ang) > 1.3f) continue;

                float k = 1f - Mathf.Abs(ang) / 1.3f;
                float radial = 1f - Mathf.Abs(d - 21f) / 6f;
                float a = Mathf.Clamp01(k * 1.1f) * Mathf.Clamp01(radial * 1.6f);
                Color32 col = Color32.Lerp(Ember, new Color32(0xFF, 0xF3, 0xD0, 255), k);
                col.a = (byte)(a * 235f);
                c.Set(x, y, col);
            }
        }
        return c;
    }

    static PixelCanvas DrawLantern()
    {
        var c = new PixelCanvas(16, 32);
        c.FillEllipse(8, 17, 8, 9, new Color32(0xE0, 0x9A, 0x3B, 35));
        c.Rect(7, 0, 2, 13, StoneDark);
        c.Rect(5, 0, 6, 2, StoneDark);
        c.Rect(4, 12, 8, 10, Stone);
        c.Rect(5, 14, 6, 6, Ember);
        c.Rect(7, 15, 2, 4, EmberPale);
        c.Rect(3, 21, 10, 3, StoneLight);
        c.Rect(6, 24, 4, 2, StoneDark);
        c.Line(5, 25, 8, 28, 1, StoneDark);
        c.Line(11, 25, 8, 28, 1, StoneDark);
        c.Outline(Ink);
        return c;
    }

    static PixelCanvas DrawHeart()
    {
        var c = new PixelCanvas(16, 16);
        Color32 red = new Color32(0xE0, 0x55, 0x4A, 255);
        Color32 dark = new Color32(0x3A, 0x12, 0x20, 255);
        Color32 hi = new Color32(0xF5, 0xA0, 0x8A, 255);

        c.FillEllipse(5, 9, 4, 4, red);
        c.FillEllipse(10, 9, 4, 4, red);
        c.FillTriangle(1, 8, 14, 8, 8, 1, red);
        c.Set(4, 11, hi);
        c.Set(5, 11, hi);
        c.Set(9, 10, hi);
        c.Outline(dark);
        return c;
    }

    static Color32 Gradient(float t, float[] pos, Color32[] col)
    {
        for (int i = 0; i < pos.Length - 1; i++)
        {
            if (t <= pos[i + 1])
            {
                float k = Mathf.InverseLerp(pos[i], pos[i + 1], t);
                return Color32.Lerp(col[i], col[i + 1], k);
            }
        }
        return col[col.Length - 1];
    }
}

// Small pixel canvas: row 0 is the bottom row, matching Texture2D SetPixels32.
public class PixelCanvas
{
    public readonly int W;
    public readonly int H;
    readonly Color32[] px;

    public PixelCanvas(int w, int h)
    {
        W = w;
        H = h;
        px = new Color32[w * h];
    }

    public Color32[] Pixels { get { return px; } }

    public void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        if (c.a == 0) return;

        int i = y * W + x;
        if (c.a == 255) { px[i] = c; return; }

        Color32 d = px[i];
        int sa = c.a;
        int da = d.a;
        int outA = sa + da * (255 - sa) / 255;
        if (outA <= 0) { px[i] = new Color32(0, 0, 0, 0); return; }

        int r = (c.r * sa + d.r * da * (255 - sa) / 255) / outA;
        int g = (c.g * sa + d.g * da * (255 - sa) / 255) / outA;
        int b = (c.b * sa + d.b * da * (255 - sa) / 255) / outA;
        px[i] = new Color32((byte)r, (byte)g, (byte)b, (byte)outA);
    }

    public void Rect(int x, int y, int w, int h, Color32 c)
    {
        for (int yy = y; yy < y + h; yy++)
            for (int xx = x; xx < x + w; xx++)
                Set(xx, yy, c);
    }

    public void FillEllipse(int cx, int cy, int rx, int ry, Color32 c)
    {
        if (rx <= 0 || ry <= 0) return;
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x - cx) / (float)rx;
                float dy = (y - cy) / (float)ry;
                if (dx * dx + dy * dy <= 1f) Set(x, y, c);
            }
        }
    }

    public void RingEllipse(int cx, int cy, int rx, int ry, int thickness, Color32 c)
    {
        int irx = Mathf.Max(1, rx - thickness);
        int iry = Mathf.Max(1, ry - thickness);
        for (int y = cy - ry; y <= cy + ry; y++)
        {
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float ox = (x - cx) / (float)rx;
                float oy = (y - cy) / (float)ry;
                float ix = (x - cx) / (float)irx;
                float iy = (y - cy) / (float)iry;
                bool outer = ox * ox + oy * oy <= 1f;
                bool inner = ix * ix + iy * iy <= 1f;
                if (outer && !inner) Set(x, y, c);
            }
        }
    }

    public void Line(int x0, int y0, int x1, int y1, int thickness, Color32 c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        int t2 = Mathf.Max(1, thickness);
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            Rect(x - t2 / 2, y - t2 / 2, t2, t2, c);
        }
    }

    public void FillTriangle(int x0, int y0, int x1, int y1, int x2, int y2, Color32 c)
    {
        int minX = Mathf.Min(x0, Mathf.Min(x1, x2));
        int maxX = Mathf.Max(x0, Mathf.Max(x1, x2));
        int minY = Mathf.Min(y0, Mathf.Min(y1, y2));
        int maxY = Mathf.Max(y0, Mathf.Max(y1, y2));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float a = Edge(x0, y0, x1, y1, x, y);
                float b = Edge(x1, y1, x2, y2, x, y);
                float d = Edge(x2, y2, x0, y0, x, y);
                bool hasNeg = a < 0f || b < 0f || d < 0f;
                bool hasPos = a > 0f || b > 0f || d > 0f;
                if (!(hasNeg && hasPos)) Set(x, y, c);
            }
        }
    }

    static float Edge(int ax, int ay, int bx, int by, int px, int py)
    {
        return (px - ax) * (by - ay) - (py - ay) * (bx - ax);
    }

    public void Outline(Color32 c)
    {
        var copy = (Color32[])px.Clone();
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                if (copy[y * W + x].a != 0) continue;
                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++)
                {
                    for (int ox = -1; ox <= 1 && !near; ox++)
                    {
                        int nx = x + ox, ny = y + oy;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                        if (copy[ny * W + nx].a > 0) near = true;
                    }
                }
                if (near) px[y * W + x] = c;
            }
        }
    }

    public void RimSide(int dir, Color32 c)
    {
        if (dir == 0) return;
        for (int y = 0; y < H; y++)
        {
            int x = dir > 0 ? W - 1 : 0;
            while (x >= 0 && x < W && px[y * W + x].a == 0) x -= dir;
            if (x < 0 || x >= W) continue;
            Set(x, y, c);
        }
    }

    public void RimTop(Color32 rim, byte alpha)
    {
        for (int x = 0; x < W; x++)
        {
            int top = -1;
            for (int y = H - 1; y >= 0; y--)
            {
                if (px[y * W + x].a > 0) { top = y; break; }
            }
            if (top < 0) continue;
            Set(x, top, rim);
            if (top + 1 < H) Set(x, top + 1, new Color32(rim.r, rim.g, rim.b, alpha));
        }
    }
}
