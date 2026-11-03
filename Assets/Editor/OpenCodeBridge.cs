// OpenCode Bridge - a tiny localhost command server that runs inside the Unity Editor.
// It lets an external agent inspect and edit the open scene, run menu items,
// control play mode, take screenshots and read logs without restarting Unity.
//
// Protocol: TCP 127.0.0.1:7777, one request per connection.
//   request:  {"cmd":"hierarchy","args":{"limit":50}}\n
//   response: {"ok":true,"data":{...}}\n
//
// This file lives in Assets/Editor, so it is stripped from player builds.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenCodeBridge
{
    [InitializeOnLoad]
    public static class Bridge
    {
        const int DefaultPort = 7777;

        static readonly int Port = ReadPort();
        static readonly ConcurrentQueue<WorkItem> Queue = new ConcurrentQueue<WorkItem>();
        static readonly List<Dictionary<string, object>> RecentLogs = new List<Dictionary<string, object>>();
        static readonly object LogLock = new object();

        static TcpListener listener;
        static int mainThreadId;
        static double nextStartAttempt;
        static bool loggedBindFailure;

        sealed class WorkItem
        {
            public Func<object> Func;
            public object Result;
            public ExceptionDispatchInfo Error;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        static Bridge()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;

            EditorApplication.update += Pump;
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            Application.logMessageReceivedThreaded += OnLog;

            EditorApplication.delayCall += TryStart;
        }

        static int ReadPort()
        {
            int p;
            var env = Environment.GetEnvironmentVariable("OPENCODE_UNITY_PORT");
            return int.TryParse(env, out p) && p > 0 ? p : DefaultPort;
        }

        static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        // ---------------------------------------------------------------- server

        static void TryStart()
        {
            if (listener != null) return;
            if (EditorApplication.timeSinceStartup < nextStartAttempt) return;
            nextStartAttempt = EditorApplication.timeSinceStartup + 5.0;

            try
            {
                var l = new TcpListener(IPAddress.Loopback, Port);
                l.Start();
                listener = l;
                var thread = new Thread(AcceptLoop) { IsBackground = true, Name = "OpenCodeBridge" };
                thread.Start();
                Debug.Log("[OpenCodeBridge] listening on 127.0.0.1:" + Port);
                loggedBindFailure = false;
            }
            catch (Exception e)
            {
                if (!loggedBindFailure)
                {
                    loggedBindFailure = true;
                    Debug.LogWarning("[OpenCodeBridge] could not start on port " + Port + ": " + e.Message);
                }
            }
        }

        static void Stop()
        {
            try
            {
                if (listener != null) listener.Stop();
            }
            catch { /* ignore */ }
            listener = null;
        }

        static void AcceptLoop()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    var l = listener;
                    if (l == null) return;
                    client = l.AcceptTcpClient();
                }
                catch
                {
                    if (listener == null) return;
                    Thread.Sleep(100);
                    continue;
                }

                ThreadPool.QueueUserWorkItem(HandleClient, client);
            }
        }

        static void HandleClient(object state)
        {
            var client = (TcpClient)state;
            try
            {
                client.NoDelay = true;
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" })
                {
                    string line = reader.ReadLine();
                    if (line == null) return;

                    Dictionary<string, object> req;
                    try
                    {
                        req = Json.Parse(line) as Dictionary<string, object>;
                        if (req == null) throw new Exception("request must be a JSON object");
                    }
                    catch (Exception e)
                    {
                        writer.WriteLine(ErrorJson("bad request: " + e.Message));
                        return;
                    }

                    string cmd = GetStr(req, "cmd", "");
                    var args = req.ContainsKey("args") ? req["args"] as Dictionary<string, object> : null;
                    int timeoutMs = (int)Math.Max(1000, Math.Min(3600000, GetNum(req, "timeoutMs", 25000)));

                    try
                    {
                        var result = OnMainThread(() => Execute(cmd, args), timeoutMs);
                        writer.WriteLine(OkJson(result));
                    }
                    catch (Exception e)
                    {
                        var inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                        writer.WriteLine(ErrorJson(inner.GetType().Name + ": " + inner.Message));
                    }
                }
            }
            catch
            {
                // client disconnected or bad encoding - nothing useful to do
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        static object OnMainThread(Func<object> fn, int timeoutMs)
        {
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId) return fn();

            var item = new WorkItem { Func = fn };
            Queue.Enqueue(item);
            if (!item.Done.Wait(timeoutMs))
                throw new Exception("timed out waiting for the Unity main thread (editor busy compiling or importing?)");
            if (item.Error != null) item.Error.Throw();
            return item.Result;
        }

        static void Pump()
        {
            if (listener == null) TryStart();

            for (int i = 0; i < 64; i++)
            {
                WorkItem item;
                if (!Queue.TryDequeue(out item)) break;
                try
                {
                    item.Result = item.Func();
                }
                catch (Exception e)
                {
                    item.Error = ExceptionDispatchInfo.Capture(e);
                }
                finally
                {
                    item.Done.Set();
                }
            }
        }

        static void OnLog(string message, string stackTrace, LogType type)
        {
            lock (LogLock)
            {
                RecentLogs.Add(new Dictionary<string, object>
                {
                    {"type", type.ToString()},
                    {"message", message},
                    {"stack", type == LogType.Error || type == LogType.Exception || type == LogType.Assert ? stackTrace : null},
                    {"time", DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}
                });
                if (RecentLogs.Count > 300) RecentLogs.RemoveAt(0);
            }
        }

        // -------------------------------------------------------------- commands

        static object Execute(string cmd, Dictionary<string, object> a)
        {
            a = a ?? new Dictionary<string, object>();
            switch ((cmd ?? "").ToLowerInvariant())
            {
                case "ping":        return Ping();
                case "hierarchy":   return Hierarchy(a);
                case "find":        return Find(a);
                case "create":      return Create(a);
                case "set":         return SetTransform(a);
                case "delete":      return Delete(a);
                case "duplicate":   return Duplicate(a);
                case "addcomponent":return AddComponent(a);
                case "removecomponent": return RemoveComponent(a);
                case "property":
                case "setproperty": return SetSerializedProperty(a);
                case "getproperties": return GetSerializedProperties(a);
                case "select":      return SelectObject(a);
                case "frame":       return FrameObject(a);
                case "menu":        return Menu(a);
                case "play":        EditorApplication.EnterPlaymode(); return new Dictionary<string, object> {{"playing", EditorApplication.isPlaying}};
                case "stop":        EditorApplication.ExitPlaymode(); return new Dictionary<string, object> {{"playing", EditorApplication.isPlaying}};
                case "pause":       EditorApplication.isPaused = !EditorApplication.isPaused; return new Dictionary<string, object> {{"paused", EditorApplication.isPaused}};
                case "save":        return Save(a);
                case "scene":       return SceneOp(a);
                case "screenshot":  return Screenshot(a);
                case "logs":        return Logs(a);
                case "assets":      return Assets(a);
                case "build":       return Build(a);
                case "invoke":      return Invoke(a);
                case "refresh":     AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate); return new Dictionary<string, object> {{"refreshed", true}};
                case "undo":        Undo.PerformUndo(); return new Dictionary<string, object> {{"ok", true}};
                case "redo":        Undo.PerformRedo(); return new Dictionary<string, object> {{"ok", true}};
                case "execute":     return ExecuteScript(a);
                default:
                    throw new Exception("unknown command '" + cmd + "'");
            }
        }

        static object Ping()
        {
            var scene = SceneManager.GetActiveScene();
            return new Dictionary<string, object>
            {
                {"editor", Application.unityVersion},
                {"project", ProjectRoot},
                {"scene", scene.path},
                {"sceneName", scene.name},
                {"sceneDirty", scene.isDirty},
                {"playmode", EditorApplication.isPlaying ? (EditorApplication.isPaused ? "paused" : "playing") : "edit"},
                {"compiling", EditorApplication.isCompiling},
                {"updating", EditorApplication.isUpdating},
                {"focused", UnityEditorInternal.InternalEditorUtility.isApplicationActive},
                {"pid", System.Diagnostics.Process.GetCurrentProcess().Id},
                {"port", Port},
                {"bridgeVersion", 2},
                {"time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}
            };
        }

        static object Hierarchy(Dictionary<string, object> a)
        {
            int limit = (int)GetNum(a, "limit", 500);
            bool inactive = GetBool(a, "inactive", true);
            var items = new List<object>();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    Walk(root.transform, scene.name, items, limit, inactive);
                    if (items.Count >= limit) break;
                }
            }

            return new Dictionary<string, object> {{"count", items.Count}, {"objects", items}};
        }

        static void Walk(Transform t, string sceneName, List<object> items, int limit, bool inactive)
        {
            if (items.Count >= limit) return;
            var go = t.gameObject;
            if (!inactive && !go.activeInHierarchy) return;

            items.Add(new Dictionary<string, object>
            {
                {"path", PathOf(t)},
                {"name", go.name},
                {"active", go.activeSelf},
                {"tag", go.tag},
                {"layer", LayerMask.LayerToName(go.layer)},
                {"position", Vec(go.transform.position)},
                {"rotation", Vec(go.transform.eulerAngles)},
                {"scale", Vec(go.transform.localScale)},
                {"components", string.Join(",", go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name))},
                {"scene", sceneName}
            });

            for (int i = 0; i < t.childCount; i++)
            {
                Walk(t.GetChild(i), sceneName, items, limit, inactive);
                if (items.Count >= limit) return;
            }
        }

        static object Find(Dictionary<string, object> a)
        {
            var go = FindObject(GetStr(a, "path", null) ?? GetStr(a, "name", null));
            if (go == null) throw new Exception("object not found");
            return Describe(go);
        }

        static object Create(Dictionary<string, object> a)
        {
            string type = (GetStr(a, "type", "empty") ?? "empty").ToLowerInvariant();
            string name = GetStr(a, "name", null);
            var pos = new Vector3((float)GetNum(a, "x", 0), (float)GetNum(a, "y", 0), (float)GetNum(a, "z", 0));

            GameObject go;
            switch (type)
            {
                case "empty":
                case "gameobject":
                    go = new GameObject(name ?? "GameObject");
                    break;
                case "sprite":
                    go = new GameObject(name ?? "Sprite");
                    var sr = go.AddComponent<SpriteRenderer>();
                    string spriteAsset = GetStr(a, "sprite", null);
                    sr.sprite = string.IsNullOrEmpty(spriteAsset)
                        ? AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd")
                        : AssetDatabase.LoadAssetAtPath<Sprite>(spriteAsset);
                    if (sr.sprite == null) throw new Exception("sprite not found: " + spriteAsset);
                    break;
                case "cube":
                case "sphere":
                case "capsule":
                case "cylinder":
                case "plane":
                case "quad":
                    go = GameObject.CreatePrimitive((PrimitiveType)Enum.Parse(typeof(PrimitiveType), Capitalize(type), true));
                    go.name = name ?? Capitalize(type);
                    break;
                case "camera":
                    go = new GameObject(name ?? "Main Camera");
                    go.AddComponent<Camera>();
                    go.tag = "MainCamera";
                    break;
                case "light":
                    go = new GameObject(name ?? "Directional Light");
                    var light = go.AddComponent<Light>();
                    light.type = LightType.Directional;
                    break;
                default:
                    throw new Exception("unsupported type '" + type + "' (empty, sprite, cube, sphere, capsule, cylinder, plane, quad, camera, light)");
            }

            go.transform.position = pos;

            string parentPath = GetStr(a, "parent", null);
            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = FindObject(parentPath);
                if (parent == null) throw new Exception("parent not found: " + parentPath);
                go.transform.SetParent(parent.transform, true);
            }

            Undo.RegisterCreatedObjectUndo(go, "OpenCode Create " + go.name);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Selection.activeGameObject = go;

            return Describe(go);
        }

        static object SetTransform(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            Undo.RecordObject(go.transform, "OpenCode Set Transform");
            Undo.RecordObject(go, "OpenCode Set Object");

            if (a.ContainsKey("name")) go.name = GetStr(a, "name", go.name);

            var t = go.transform;
            if (HasAny(a, "x", "y", "z"))
            {
                var p = t.position;
                t.position = new Vector3((float)GetNum(a, "x", p.x), (float)GetNum(a, "y", p.y), (float)GetNum(a, "z", p.z));
            }
            if (HasAny(a, "rx", "ry", "rz"))
            {
                var r = t.eulerAngles;
                t.eulerAngles = new Vector3((float)GetNum(a, "rx", r.x), (float)GetNum(a, "ry", r.y), (float)GetNum(a, "rz", r.z));
            }
            if (HasAny(a, "sx", "sy", "sz"))
            {
                var s = t.localScale;
                t.localScale = new Vector3((float)GetNum(a, "sx", s.x), (float)GetNum(a, "sy", s.y), (float)GetNum(a, "sz", s.z));
            }
            if (a.ContainsKey("active")) go.SetActive(GetBool(a, "active", true));
            if (a.ContainsKey("tag")) go.tag = GetStr(a, "tag", go.tag);
            if (a.ContainsKey("layer")) go.layer = LayerMask.NameToLayer(GetStr(a, "layer", "Default"));

            if (a.ContainsKey("parent"))
            {
                string parentPath = GetStr(a, "parent", null);
                if (string.IsNullOrEmpty(parentPath)) t.SetParent(null, true);
                else
                {
                    var parent = FindObject(parentPath);
                    if (parent == null) throw new Exception("parent not found: " + parentPath);
                    t.SetParent(parent.transform, true);
                }
            }

            EditorSceneManager.MarkSceneDirty(go.scene);
            return Describe(go);
        }

        static object Delete(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            string path = PathOf(go.transform);
            Undo.DestroyObjectImmediate(go);
            return new Dictionary<string, object> {{"deleted", path}};
        }

        static object Duplicate(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            var copy = UnityEngine.Object.Instantiate(go, go.transform.parent);
            copy.name = go.name + " (Copy)";
            Undo.RegisterCreatedObjectUndo(copy, "OpenCode Duplicate");
            EditorSceneManager.MarkSceneDirty(copy.scene);
            Selection.activeGameObject = copy;
            return Describe(copy);
        }

        static object AddComponent(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            string name = GetStr(a, "component", null);
            if (string.IsNullOrEmpty(name)) throw new Exception("'component' is required");

            var type = ResolveComponentType(name);
            var comp = Undo.AddComponent(go, type);
            if (comp == null) throw new Exception("failed to add component " + name);

            EditorSceneManager.MarkSceneDirty(go.scene);
            return new Dictionary<string, object> {{"added", type.Name}, {"path", PathOf(go.transform)}};
        }

        static object RemoveComponent(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            string name = GetStr(a, "component", null);
            var comp = go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == name);
            if (comp == null) throw new Exception("component not found: " + name);
            Undo.DestroyObjectImmediate(comp);
            EditorSceneManager.MarkSceneDirty(go.scene);
            return new Dictionary<string, object> {{"removed", name}};
        }

        static object SetSerializedProperty(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            string componentName = GetStr(a, "component", null);
            string propName = GetStr(a, "prop", null);

            UnityEngine.Object target;
            if (string.IsNullOrEmpty(componentName) || componentName == "GameObject")
            {
                target = go;
            }
            else
            {
                target = go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == componentName);
                if (target == null) throw new Exception("component not found: " + componentName);
            }

            Undo.RecordObject(target, "OpenCode Set Property");
            var so = new SerializedObject(target);

            if (a.ContainsKey("props"))
            {
                var props = a["props"] as Dictionary<string, object>;
                if (props == null) throw new Exception("'props' must be an object");
                foreach (var kv in props)
                {
                    var sp = so.FindProperty(kv.Key);
                    if (sp == null) throw new Exception("property not found: " + kv.Key);
                    SetPropertyValue(sp, kv.Value);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(propName)) throw new Exception("'prop' is required");
                var sp = so.FindProperty(propName);
                if (sp == null) throw new Exception("property not found: " + propName);
                SetPropertyValue(sp, a.ContainsKey("value") ? a["value"] : null);
            }

            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(go.scene);
            EditorUtility.SetDirty(target);
            return new Dictionary<string, object> {{"updated", PathOf(go.transform)}};
        }

        static object GetSerializedProperties(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            string componentName = GetStr(a, "component", null);
            UnityEngine.Object target = string.IsNullOrEmpty(componentName) || componentName == "GameObject"
                ? go
                : go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == componentName);
            if (target == null) throw new Exception("component not found: " + componentName);

            var so = new SerializedObject(target);
            var result = new List<object>();
            var it = so.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                result.Add(new Dictionary<string, object>
                {
                    {"prop", it.propertyPath},
                    {"type", it.propertyType.ToString()},
                    {"value", ReadProperty(it)}
                });
            }
            return new Dictionary<string, object> {{"component", target.GetType().Name}, {"properties", result}};
        }

        static object ReadProperty(SerializedProperty p)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.intValue;
                case SerializedPropertyType.Boolean: return p.boolValue;
                case SerializedPropertyType.Float: return (double)p.floatValue;
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Color: return Vec(p.colorValue);
                case SerializedPropertyType.ObjectReference: return p.objectReferenceValue == null ? null : (object)p.objectReferenceValue.name;
                case SerializedPropertyType.Enum: return p.enumNames.Length > p.enumValueIndex && p.enumValueIndex >= 0 ? (object)p.enumNames[p.enumValueIndex] : p.enumValueIndex;
                case SerializedPropertyType.Vector2: return Vec(p.vector2Value);
                case SerializedPropertyType.Vector3: return Vec(p.vector3Value);
                case SerializedPropertyType.Vector4: return new List<object> {p.vector4Value.x, p.vector4Value.y, p.vector4Value.z, p.vector4Value.w};
                case SerializedPropertyType.Rect: return new Dictionary<string, object> {{"x", p.rectValue.x}, {"y", p.rectValue.y}, {"width", p.rectValue.width}, {"height", p.rectValue.height}};
                default: return null;
            }
        }

        static object SelectObject(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
            return Describe(go);
        }

        static object FrameObject(Dictionary<string, object> a)
        {
            var go = RequireObject(a);
            Selection.activeGameObject = go;
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                var renderers = go.GetComponentsInChildren<Renderer>();
                var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.one);
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                view.Frame(bounds, false);
                view.Repaint();
            }
            return Describe(go);
        }

        static object Menu(Dictionary<string, object> a)
        {
            string path = GetStr(a, "path", null);
            if (string.IsNullOrEmpty(path)) throw new Exception("'path' is required, e.g. \"File/Save\"");
            bool ok = EditorApplication.ExecuteMenuItem(path);
            if (!ok) throw new Exception("menu item not found or disabled: " + path);
            return new Dictionary<string, object> {{"executed", path}};
        }

        static object Save(Dictionary<string, object> a)
        {
            var scene = SceneManager.GetActiveScene();
            string path = GetStr(a, "path", null);
            bool ok = string.IsNullOrEmpty(path)
                ? EditorSceneManager.SaveScene(scene)
                : EditorSceneManager.SaveScene(scene, NormalizeAssetPath(path));
            if (!ok) throw new Exception("scene save failed (is the path inside Assets/?)");
            return new Dictionary<string, object> {{"saved", SceneManager.GetActiveScene().path}};
        }

        static object SceneOp(Dictionary<string, object> a)
        {
            string action = (GetStr(a, "action", "info") ?? "info").ToLowerInvariant();
            switch (action)
            {
                case "info":
                    var scene = SceneManager.GetActiveScene();
                    return new Dictionary<string, object> {{"path", scene.path}, {"name", scene.name}, {"dirty", scene.isDirty}};
                case "new":
                    EditorSceneManager.SaveOpenScenes();
                    var fresh = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    return new Dictionary<string, object> {{"created", fresh.name}};
                case "open":
                    string openPath = NormalizeAssetPath(GetStr(a, "path", null));
                    EditorSceneManager.SaveOpenScenes();
                    var opened = EditorSceneManager.OpenScene(openPath, OpenSceneMode.Single);
                    return new Dictionary<string, object> {{"opened", opened.path}};
                case "saveas":
                    string savePath = NormalizeAssetPath(GetStr(a, "path", null));
                    EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), savePath);
                    return new Dictionary<string, object> {{"saved", savePath}};
                default: throw new Exception("unknown scene action '" + action + "'");
            }
        }

        static object Screenshot(Dictionary<string, object> a)
        {
            string view = (GetStr(a, "view", "game") ?? "game").ToLowerInvariant();
            int width = (int)GetNum(a, "width", 1280);
            int height = (int)GetNum(a, "height", 720);

            Camera cam = null;
            if (view == "scene")
            {
                if (SceneView.lastActiveSceneView != null) cam = SceneView.lastActiveSceneView.camera;
            }
            else
            {
                cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }
            if (cam == null) throw new Exception("no camera found for view '" + view + "'");

            string path = GetStr(a, "path", null);
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("Screenshots", "shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".png");
            path = Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }

            return new Dictionary<string, object> {{"path", path}, {"width", width}, {"height", height}, {"view", view}};
        }

        static object Logs(Dictionary<string, object> a)
        {
            int count = (int)GetNum(a, "count", 40);
            string filter = (GetStr(a, "type", "all") ?? "all").ToLowerInvariant();

            var entries = new List<object>();
            lock (LogLock)
            {
                foreach (var e in RecentLogs)
                {
                    string t = ((string)e["type"]).ToLowerInvariant();
                    if (filter == "error" && !(t == "error" || t == "exception" || t == "assert")) continue;
                    if (filter == "warning" && t != "warning") continue;
                    if (filter == "info" && t != "log") continue;
                    entries.Add(e);
                }
            }
            if (entries.Count > count) entries = entries.Skip(entries.Count - count).Cast<object>().ToList();

            return new Dictionary<string, object>
            {
                {"consoleLog", Application.consoleLogPath},
                {"count", entries.Count},
                {"entries", entries}
            };
        }

        static object Assets(Dictionary<string, object> a)
        {
            string folder = GetStr(a, "folder", "Assets");
            if (!folder.StartsWith("Assets")) throw new Exception("'folder' must start with Assets/");
            int limit = (int)GetNum(a, "limit", 200);

            var paths = AssetDatabase.FindAssets("", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !AssetDatabase.IsValidFolder(p))
                .Take(limit)
                .ToList();

            return new Dictionary<string, object> {{"count", paths.Count}, {"assets", paths}};
        }

        // Runs a C# snippet through Roslyn? Not available. This is a placeholder that
        // reports the feature is intentionally unsupported so callers get a clear error.
        static object ExecuteScript(Dictionary<string, object> a)
        {
            throw new Exception("'execute' is not available; write a script under Assets/Editor and use a command instead");
        }

        // ---------------------------------------------------------------- helpers

        static object Build(Dictionary<string, object> a)
        {
            var scenes = new List<string>();
            if (a.ContainsKey("scenes"))
            {
                var raw = a["scenes"] as List<object>;
                if (raw == null) throw new Exception("'scenes' must be an array of asset paths");
                foreach (var s in raw) scenes.Add(NormalizeAssetPath(s == null ? null : s.ToString()));
            }
            if (scenes.Count == 0)
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)
                    .Where(p => !string.IsNullOrEmpty(p)).ToList();
            if (scenes.Count == 0)
                throw new Exception("no scenes are enabled in Build Settings; pass scenes=[\"Assets/Scenes/SampleScene.unity\"]");

            var target = EditorUserBuildSettings.activeBuildTarget;
            string path = GetStr(a, "path", null);
            if (string.IsNullOrEmpty(path))
                path = Path.Combine("Builds", Application.productName, Application.productName + ExtensionFor(target));
            path = Path.IsPathRooted(path) ? path : Path.Combine(ProjectRoot, path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = path,
                target = target,
                options = GetBool(a, "development", false) ? BuildOptions.Development : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            return new Dictionary<string, object>
            {
                {"result", summary.result.ToString()},
                {"target", summary.platform.ToString()},
                {"output", path},
                {"sizeBytes", (long)summary.totalSize},
                {"errors", summary.totalErrors},
                {"warnings", summary.totalWarnings},
                {"durationSeconds", Math.Round(summary.totalTime.TotalSeconds, 1)},
                {"steps", report.steps.Length}
            };
        }

        static string ExtensionFor(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: return ".exe";
                case BuildTarget.StandaloneOSX: return ".app";
                case BuildTarget.Android: return ".apk";
                default: return "";
            }
        }

        static object Invoke(Dictionary<string, object> a)
        {
            string typeName = GetStr(a, "type", null);
            string methodName = GetStr(a, "method", null);
            if (string.IsNullOrEmpty(typeName)) throw new Exception("'type' is required");
            if (string.IsNullOrEmpty(methodName)) throw new Exception("'method' is required");
            var argsDict = a.ContainsKey("args") ? a["args"] as Dictionary<string, object> : null;

            Type type = null;
            Type shortMatch = null;
            int shortCount = 0;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                foreach (var t in types)
                {
                    if (t.FullName == typeName) { type = t; break; }
                    if (t.Name == typeName) { shortMatch = t; shortCount++; }
                }
                if (type != null) break;
            }
            if (type == null && shortCount == 1) type = shortMatch;
            if (type == null)
                throw new Exception(shortCount > 1 ? "type name is ambiguous: " + typeName : "type not found: " + typeName);

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var methods = type.GetMethods(flags).Where(m => m.Name == methodName).ToList();
            if (methods.Count == 0) throw new Exception("method not found: " + type.FullName + "." + methodName);

            MethodInfo method = methods.FirstOrDefault(m => m.IsStatic);
            object target = null;
            if (method == null)
            {
                string path = GetStr(a, "path", null);
                if (string.IsNullOrEmpty(path))
                    throw new Exception("'" + methodName + "' is an instance method; pass path=<gameobject>");
                var go = FindObject(path);
                if (go == null) throw new Exception("object not found: " + path);
                var comp = go.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType() == type);
                if (comp == null) throw new Exception("component " + type.Name + " not found on " + path);
                target = comp;
            }

            var ps = method.GetParameters();
            object[] callArgs;
            if (ps.Length == 0) callArgs = new object[0];
            else if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(typeof(Dictionary<string, object>)))
                callArgs = new object[] { argsDict ?? new Dictionary<string, object>() };
            else
                throw new Exception("only methods with zero parameters or a single Dictionary<string,object> parameter are supported");

            var result = method.Invoke(target, callArgs);
            return new Dictionary<string, object>
            {
                {"type", type.FullName},
                {"method", method.Name},
                {"result", Simplify(result)}
            };
        }

        static object Simplify(object v)
        {
            if (v == null) return null;
            if (v is string || v is bool || v is int || v is long || v is float || v is double || v is decimal) return v;
            if (v is UnityEngine.Object)
            {
                var uo = (UnityEngine.Object)v;
                var go = uo as GameObject;
                return go != null ? (object)Describe(go) : uo.name;
            }
            var enumerable = v as System.Collections.IEnumerable;
            if (enumerable != null)
            {
                var list = new List<object>();
                int n = 0;
                foreach (var item in enumerable)
                {
                    list.Add(Simplify(item));
                    if (++n >= 200) break;
                }
                return list;
            }
            return v.ToString();
        }

        static GameObject RequireObject(Dictionary<string, object> a)
        {
            string path = GetStr(a, "path", null) ?? GetStr(a, "name", null);
            if (string.IsNullOrEmpty(path)) throw new Exception("'path' is required");
            var go = FindObject(path);
            if (go == null) throw new Exception("object not found: " + path);
            return go;
        }

        static GameObject FindObject(string pathOrName)
        {
            if (string.IsNullOrEmpty(pathOrName)) return null;
            string target = pathOrName.Replace('\\', '/').Trim('/');

            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i));

            // exact path first
            foreach (var scene in scenes)
            {
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var hit = FindByPath(root.transform, target);
                    if (hit != null) return hit;
                }
            }
            // then by name
            foreach (var scene in scenes)
            {
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var hit = FindByName(root.transform, target);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        static GameObject FindByPath(Transform t, string target)
        {
            if (PathOf(t) == target) return t.gameObject;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindByPath(t.GetChild(i), target);
                if (hit != null) return hit;
            }
            return null;
        }

        static GameObject FindByName(Transform t, string target)
        {
            if (t.name == target) return t.gameObject;
            for (int i = 0; i < t.childCount; i++)
            {
                var hit = FindByName(t.GetChild(i), target);
                if (hit != null) return hit;
            }
            return null;
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static Dictionary<string, object> Describe(GameObject go)
        {
            var t = go.transform;
            return new Dictionary<string, object>
            {
                {"path", PathOf(t)},
                {"name", go.name},
                {"active", go.activeSelf},
                {"tag", go.tag},
                {"layer", LayerMask.LayerToName(go.layer)},
                {"position", Vec(t.position)},
                {"rotation", Vec(t.eulerAngles)},
                {"scale", Vec(t.localScale)},
                {"components", go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToList()}
            };
        }

        static Type ResolveComponentType(string name)
        {
            foreach (var t in UnityEditor.TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (!t.IsAbstract && (t.Name == name || t.FullName == name))
                    return t;
            }
            throw new Exception("component type not found: " + name);
        }

        static void SetPropertyValue(SerializedProperty p, object v)
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: p.intValue = Convert.ToInt32(v, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.LayerMask: p.intValue = Convert.ToInt32(v, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.Boolean: p.boolValue = ToBool(v); break;
                case SerializedPropertyType.Float: p.floatValue = (float)Convert.ToDouble(v, CultureInfo.InvariantCulture); break;
                case SerializedPropertyType.String: p.stringValue = v == null ? "" : v.ToString(); break;
                case SerializedPropertyType.Color: p.colorValue = ToColor(v); break;
                case SerializedPropertyType.Vector2:
                    {
                        var v3 = ToVector3(v);
                        p.vector2Value = new Vector2(v3.x, v3.y);
                        break;
                    }
                case SerializedPropertyType.Vector3: p.vector3Value = ToVector3(v); break;
                case SerializedPropertyType.Vector4:
                    {
                        var v3 = ToVector3(v);
                        p.vector4Value = new Vector4(v3.x, v3.y, v3.z, (float)GetNum(v is Dictionary<string, object> d ? d : new Dictionary<string, object>(), "w", 0));
                        break;
                    }
                case SerializedPropertyType.Enum:
                    if (v is string)
                    {
                        int idx = Array.IndexOf(p.enumNames, (string)v);
                        if (idx < 0) throw new Exception("enum value not found: " + v + " (options: " + string.Join(", ", p.enumNames) + ")");
                        p.enumValueIndex = idx;
                    }
                    else p.enumValueIndex = Convert.ToInt32(v, CultureInfo.InvariantCulture);
                    break;
                case SerializedPropertyType.ObjectReference:
                    if (v == null || (v is string && (string)v == "null"))
                    {
                        p.objectReferenceValue = null;
                    }
                    else if (v is string)
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>((string)v);
                        if (asset == null) throw new Exception("asset not found: " + v);
                        p.objectReferenceValue = asset;
                    }
                    else throw new Exception("object reference value must be an asset path or null");
                    break;
                case SerializedPropertyType.Generic:
                    var dict = v as Dictionary<string, object>;
                    if (dict == null) throw new Exception("generic property needs an object value");
                    foreach (var kv in dict)
                    {
                        var child = p.FindPropertyRelative(kv.Key);
                        if (child == null) throw new Exception("sub-property not found: " + kv.Key);
                        SetPropertyValue(child, kv.Value);
                    }
                    break;
                default:
                    throw new Exception("unsupported property type " + p.propertyType);
            }
        }

        static bool ToBool(object v)
        {
            if (v is bool) return (bool)v;
            if (v is double) return Math.Abs((double)v) > double.Epsilon;
            if (v is string) return bool.Parse((string)v);
            return Convert.ToBoolean(v, CultureInfo.InvariantCulture);
        }

        static Color ToColor(object v)
        {
            if (v is string)
            {
                Color c;
                if (ColorUtility.TryParseHtmlString((string)v, out c)) return c;
                throw new Exception("invalid color string: " + v);
            }
            if (v is Dictionary<string, object>)
            {
                var d = (Dictionary<string, object>)v;
                return new Color((float)GetNum(d, "r", 1), (float)GetNum(d, "g", 1), (float)GetNum(d, "b", 1), (float)GetNum(d, "a", 1));
            }
            if (v is List<object>)
            {
                var l = (List<object>)v;
                return new Color(F(l, 0, 1), F(l, 1, 1), F(l, 2, 1), F(l, 3, 1));
            }
            throw new Exception("color must be \"#RRGGBB\", {r,g,b,a} or [r,g,b,a]");
        }

        static Vector3 ToVector3(object v)
        {
            if (v is Dictionary<string, object>)
            {
                var d = (Dictionary<string, object>)v;
                return new Vector3((float)GetNum(d, "x", 0), (float)GetNum(d, "y", 0), (float)GetNum(d, "z", 0));
            }
            if (v is List<object>)
            {
                var l = (List<object>)v;
                return new Vector3(F(l, 0, 0), F(l, 1, 0), F(l, 2, 0));
            }
            throw new Exception("expected vector {x,y,z} or [x,y,z]");
        }

        static float F(List<object> list, int index, float fallback)
        {
            return index < list.Count ? (float)Convert.ToDouble(list[index], CultureInfo.InvariantCulture) : fallback;
        }

        static List<object> Vec(Vector3 v)
        {
            return new List<object> {Round(v.x), Round(v.y), Round(v.z)};
        }

        static List<object> Vec(Vector2 v)
        {
            return new List<object> {Round(v.x), Round(v.y)};
        }

        static List<object> Vec(Color c)
        {
            return new List<object> {Round(c.r), Round(c.g), Round(c.b), Round(c.a)};
        }

        static double Round(float f)
        {
            return Math.Round(f, 4);
        }

        static string Capitalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new Exception("'path' is required");
            path = path.Replace('\\', '/');
            if (!path.StartsWith("Assets/") && !path.StartsWith("Assets"))
                throw new Exception("asset path must start with Assets/");
            return path;
        }

        static bool HasAny(Dictionary<string, object> a, params string[] keys)
        {
            foreach (var k in keys) if (a.ContainsKey(k)) return true;
            return false;
        }

        static string GetStr(Dictionary<string, object> a, string key, string fallback)
        {
            object v;
            return a != null && a.TryGetValue(key, out v) && v != null ? v.ToString() : fallback;
        }

        static double GetNum(Dictionary<string, object> a, string key, double fallback)
        {
            object v;
            if (a == null || !a.TryGetValue(key, out v) || v == null) return fallback;
            return Convert.ToDouble(v, CultureInfo.InvariantCulture);
        }

        static bool GetBool(Dictionary<string, object> a, string key, bool fallback)
        {
            object v;
            if (a == null || !a.TryGetValue(key, out v) || v == null) return fallback;
            return ToBool(v);
        }

        static string OkJson(object data)
        {
            return "{\"ok\":true,\"data\":" + Json.Write(data) + "}";
        }

        static string ErrorJson(string message)
        {
            return "{\"ok\":false,\"error\":" + Json.Quote(message) + "}";
        }
    }

    // Minimal JSON reader/writer. Values come back as Dictionary<string,object>,
    // List<object>, string, double, bool or null.
    public static class Json
    {
        public static object Parse(string text)
        {
            int i = 0;
            var value = ParseValue(text, ref i);
            SkipWs(text, ref i);
            if (i < text.Length) throw new FormatException("unexpected trailing characters at " + i);
            return value;
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end of input");

            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }

            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':' at " + i);
                i++;
                result[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return result; }
                throw new FormatException("expected ',' or '}' at " + i);
            }
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }

            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return result; }
                throw new FormatException("expected ',' or ']' at " + i);
            }
        }

        static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("expected string at " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }

                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("bad unicode escape");
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: throw new FormatException("bad escape \\" + esc);
                }
            }
            throw new FormatException("unterminated string");
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '-' || s[i] == '+')) i++;
            var token = s.Substring(start, i - start);
            double d;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("bad number '" + token + "'");
            return d;
        }

        static void Expect(string s, ref int i, string literal)
        {
            if (i + literal.Length > s.Length || s.Substring(i, literal.Length) != literal)
                throw new FormatException("expected " + literal + " at " + i);
            i += literal.Length;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }

            if (value is string) { sb.Append(Quote((string)value)); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is int || value is long || value is short || value is byte) { sb.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture)); return; }
            if (value is float || value is double || value is decimal)
            {
                double d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
                if (Math.Abs(d - Math.Round(d)) < 1e-12 && Math.Abs(d) < 1e15) sb.Append(((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture));
                else sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (value is IDictionary<string, object>)
            {
                sb.Append('{');
                bool first = true;
                foreach (var kv in (IDictionary<string, object>)value)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    sb.Append(Quote(kv.Key));
                    sb.Append(':');
                    WriteValue(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            if (value is System.Collections.IEnumerable)
            {
                sb.Append('[');
                bool first = true;
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteValue(sb, item);
                }
                sb.Append(']');
                return;
            }
            sb.Append(Quote(value.ToString()));
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
