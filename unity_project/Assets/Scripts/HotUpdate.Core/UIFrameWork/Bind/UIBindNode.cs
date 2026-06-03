// C# UI Binding System - Hybrid Runtime + Strong Binder + Nested Binder
// -----------------------------------------------------------------------------
// 目标：
// 1. 类似 vBindBehaviour，但更适合 C#。
// 2. 支持快速开发期字符串访问：Btn("BtnClose"), Txt("TxtTitle")。
// 3. 支持稳定后强类型访问：B.BtnClose.OnClick(...), B.TxtTitle.Text = "..."。
// 4. 支持嵌套 Binder：B.RewardItem.TxtCount.Text。
// 5. 支持 Prefab 保存时自动刷新绑定和生成 .g.cs。
//
// 推荐拆分文件：
// Runtime/UIBindNode.cs
// Runtime/CSharpUIBindBehaviour.cs
// Runtime/UIBindData.cs
// Runtime/UIBindRefs.cs
// Runtime/UIViewBinder.cs
// Runtime/UIViewBinderFactory.cs
// Runtime/ViewBaseBindingExtensions.cs
// Editor/CSharpUIBindBehaviourEditor.cs
// Editor/CSharpUIBindAutoProcessor.cs
// Editor/CSharpUIBindCodeGenerator.cs
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

    // ============================================================
    // 0. Utility
    // ============================================================

    public static class UIBindNameUtility
    {
        public static string CleanGameObjectName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unnamed";

            return raw
                .Replace("(Clone)", string.Empty)
                .Replace(" ", string.Empty)
                .Trim();
        }

        public static string ToSafeIdentifier(string raw)
        {
            raw = CleanGameObjectName(raw);

            var sb = new System.Text.StringBuilder();
            if (!char.IsLetter(raw[0]) && raw[0] != '_')
                sb.Append('_');

            foreach (var c in raw)
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }

            return sb.ToString();
        }

        public static string GetRelativePath(Transform root, Transform target)
        {
            if (root == target)
                return string.Empty;

            var stack = new Stack<string>();
            var t = target;
            while (t != null && t != root)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", stack);
        }
    }

    // ============================================================
    // 1. Bind Node
    //    挂在需要导出的节点上。
    // ============================================================

    [DisallowMultipleComponent]
    public sealed class UIBindNode : MonoBehaviour
    {
        [Tooltip("导出字段名。为空则使用 GameObject 名。建议 PascalCase，例如 BtnClose / TxtTitle。")]
        public string BindName;

        [Tooltip("是否导出。")]
        public bool Export = true;

        [Tooltip("是否强制当作子 Binder。通常节点上有 CSharpUIBindBehaviour 时会自动识别。")]
        public bool IsSubBinder;

        [Tooltip("子 Binder 强类型完整类型名。为空则使用子 CSharpUIBindBehaviour 的 GeneratedNamespace + GeneratedClassName。")]
        public string NestedBinderTypeName;

        [TextArea]
        public string Comment;

        public string Key
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(BindName))
                    return UIBindNameUtility.ToSafeIdentifier(BindName.Trim());

                return UIBindNameUtility.ToSafeIdentifier(gameObject.name);
            }
        }
    }

    // ============================================================
    // 2. Serialized Data
    // ============================================================

    [Serializable]
    public sealed class UIBindComponentRef
    {
        public string Alias;
        public string TypeName;
        public Component Component;
    }

    [Serializable]
    public sealed class UIBindItem
    {
        public string Key;
        public string Path;
        public string MainAlias;

        public bool IsSubBinder;
        public string NestedBinderTypeName;

        public GameObject GameObject;
        public CSharpUIBindBehaviour SubBinder;

        public List<UIBindComponentRef> Components = new();
    }

    // ============================================================
    // 3. Runtime Refs
    //    业务使用的舒服包装层。
    // ============================================================

    public class UIObjectRef
    {
        private readonly Dictionary<string, Component> _components = new();
        private readonly Dictionary<Type, UIViewBinder> _binderCache = new();

        public string Key { get; }
        public string Path { get; }
        public string MainAlias { get; }
        public bool IsSubBinder { get; }
        public string NestedBinderTypeName { get; }
        public GameObject GameObject { get; }
        public Transform Transform { get; }
        public RectTransform RectTransform { get; }
        public CSharpUIBindBehaviour SubBinderSource { get; }

        public UIObjectRef(UIBindItem item)
        {
            Key = item.Key;
            Path = item.Path;
            MainAlias = item.MainAlias;
            IsSubBinder = item.IsSubBinder;
            NestedBinderTypeName = item.NestedBinderTypeName;
            GameObject = item.GameObject;
            Transform = GameObject != null ? GameObject.transform : null;
            RectTransform = Transform as RectTransform;
            SubBinderSource = item.SubBinder;

            if (item.Components == null)
                return;

            foreach (var c in item.Components)
            {
                if (c == null || c.Component == null || string.IsNullOrEmpty(c.Alias))
                    continue;

                _components[c.Alias] = c.Component;
            }
        }

        public bool TryGet<T>(out T component) where T : Component
        {
            foreach (var c in _components.Values)
            {
                if (c is T t)
                {
                    component = t;
                    return true;
                }
            }

            component = null;
            return false;
        }

        public T Get<T>() where T : Component
        {
            if (TryGet<T>(out var component))
                return component;

            throw new Exception($"[UIObjectRef] Component not found. key={Key}, type={typeof(T).Name}");
        }

        public bool TryGetAlias<T>(string alias, out T component) where T : Component
        {
            if (_components.TryGetValue(alias, out var c) && c is T t)
            {
                component = t;
                return true;
            }

            component = null;
            return false;
        }

        public T GetAlias<T>(string alias) where T : Component
        {
            if (TryGetAlias<T>(alias, out var component))
                return component;

            throw new Exception($"[UIObjectRef] Component alias not found. key={Key}, alias={alias}, type={typeof(T).Name}");
        }

        public UIViewBinder GetBinder()
        {
            return GetBinder<UIViewBinder>();
        }

        public TBinder GetBinder<TBinder>() where TBinder : UIViewBinder
        {
            if (!IsSubBinder || SubBinderSource == null)
                throw new Exception($"[UIObjectRef] This object is not a sub binder: {Key}");

            var type = typeof(TBinder);
            if (_binderCache.TryGetValue(type, out var cached))
                return (TBinder)cached;

            var binder = (TBinder)Activator.CreateInstance(type, SubBinderSource);
            _binderCache[type] = binder;
            return binder;
        }

        public void SetActive(bool active)
        {
            if (GameObject != null)
                GameObject.SetActive(active);
        }

        public T AddComponent<T>() where T : Component
        {
            return GameObject.AddComponent<T>();
        }

        public Button Button => Get<Button>();
        public Image Image => Get<Image>();
        public RawImage RawImage => Get<RawImage>();
        public Text Text => Get<Text>();
        public TextMeshProUGUI TMPText => Get<TextMeshProUGUI>();
        public TMP_InputField TMPInput  => Get<TMP_InputField>();
        public Toggle Toggle => Get<Toggle>();
        public Slider Slider => Get<Slider>();
        public ScrollRect ScrollRect => Get<ScrollRect>();
        public CanvasGroup CanvasGroup => Get<CanvasGroup>();
        public Animator Animator => Get<Animator>();
    }

    public sealed class UIButtonRef : UIObjectRef
    {
        public UIButtonRef(UIBindItem item) : base(item) { }

        public void OnClick(Action action)
        {
            Button.onClick.AddListener(() => action?.Invoke());
        }

        public void OnClick(Func<Task> action)
        {
            Button.onClick.AddListener(() => action?.Invoke());
        }

        public bool Interactable
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }
    }

    public sealed class UITextRef : UIObjectRef
    {
        public UITextRef(UIBindItem item) : base(item) { }

        public string Value
        {
            get
            {
                if (TryGet<Text>(out var text)) return text.text;
                if (TryGet<TextMeshProUGUI>(out var tmp)) return tmp.text;
                return string.Empty;
            }
            set
            {
                if (TryGet<Text>(out var text)) text.text = value;
                else if (TryGet<TextMeshProUGUI>(out var tmp)) tmp.text = value;
                else throw new Exception($"[UITextRef] No Text or TMPText on {Key}");
            }
        }
    }

    public sealed class UIImageRef : UIObjectRef
    {
        public UIImageRef(UIBindItem item) : base(item) { }

        public Sprite Sprite
        {
            get => Image.sprite;
            set => Image.sprite = value;
        }

        public Color Color
        {
            get => Image.color;
            set => Image.color = value;
        }
    }

    public sealed class UIScrollRef : UIObjectRef
    {
        public UIScrollRef(UIBindItem item) : base(item) { }

        public Vector2 NormalizedPosition
        {
            get => ScrollRect.normalizedPosition;
            set => ScrollRect.normalizedPosition = value;
        }
    }

    // ============================================================
    // 4. UIViewBinder
    //    同时支持 Raw 字符串访问和强类型生成访问。
    // ============================================================

    public class UIViewBinder
    {
        private readonly Dictionary<string, UIObjectRef> _map = new();

        public CSharpUIBindBehaviour Source { get; }

        public UIViewBinder(CSharpUIBindBehaviour source)
        {
            Source = source;

            foreach (var item in source.Items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Key) || item.GameObject == null)
                    continue;

                _map[item.Key] = CreateRefByMainAlias(item);
            }
        }

        public UIObjectRef this[string key] => Get(key);

        public UIObjectRef Get(string key)
        {
            if (_map.TryGetValue(key, out var obj))
                return obj;

            throw new Exception($"Bind key not found: {key}");
        }

        public bool TryGet(string key, out UIObjectRef obj)
        {
            return _map.TryGetValue(key, out obj);
        }

        public T Get<T>(string key) where T : Component
        {
            return Get(key).Get<T>();
        }

        public UIButtonRef Btn(string key)
        {
            return CastRef<UIButtonRef>(key);
        }

        public UITextRef Txt(string key)
        {
            return CastRef<UITextRef>(key);
        }

        public UIImageRef Img(string key)
        {
            return CastRef<UIImageRef>(key);
        }

        public UIScrollRef Scroll(string key)
        {
            return CastRef<UIScrollRef>(key);
        }

        public UIViewBinder GetBinder(string key)
        {
            return Get(key).GetBinder();
        }

        public TBinder GetBinder<TBinder>(string key) where TBinder : UIViewBinder
        {
            return Get(key).GetBinder<TBinder>();
        }

        private TRef CastRef<TRef>(string key) where TRef : UIObjectRef
        {
            var obj = Get(key);
            if (obj is TRef typed)
                return typed;

            throw new Exception($"[UIViewBinder] Bind key={key} is {obj.GetType().Name}, not {typeof(TRef).Name}");
        }

        private static UIObjectRef CreateRefByMainAlias(UIBindItem item)
        {
            if (item.IsSubBinder)
                return new UIObjectRef(item);

            return item.MainAlias switch
            {
                "Button" => new UIButtonRef(item),
                "Text" => new UITextRef(item),
                "TMPText" => new UITextRef(item),
                "Image" => new UIImageRef(item),
                "RawImage" => new UIObjectRef(item),
                "ScrollRect" => new UIScrollRef(item),
                _ => new UIObjectRef(item)
            };
        }
    }

    // ============================================================
    // 5. Binder Factory
    //    UIWindow 创建 View 时用。
    // ============================================================

    public static class UIViewBinderFactory
    {
        private static readonly Dictionary<Type, Type> RegisteredBinderTypes = new();

        public static void Register<TView, TBinder>()
            where TBinder : UIViewBinder
        {
            RegisteredBinderTypes[typeof(TView)] = typeof(TBinder);
        }

        public static UIViewBinder Create(Type viewType, CSharpUIBindBehaviour source)
        {
            if (RegisteredBinderTypes.TryGetValue(viewType, out var binderType))
                return CreateBinder(binderType, source);

            var inferredType = TryInferBinderTypeFromViewBase(viewType);
            if (inferredType != null)
                return CreateBinder(inferredType, source);

            var generatedType = Type.GetType(source.BinderFullTypeName);
            if (generatedType != null && typeof(UIViewBinder).IsAssignableFrom(generatedType))
                return CreateBinder(generatedType, source);

            return new UIViewBinder(source);
        }

        private static UIViewBinder CreateBinder(Type binderType, CSharpUIBindBehaviour source)
        {
            try
            {
                return (UIViewBinder)Activator.CreateInstance(binderType, new object[] { source });
            }
            catch (Exception e)
            {
                throw new Exception($"[UIViewBinderFactory] Create binder failed: {binderType.FullName}. Need ctor(CSharpUIBindBehaviour).", e);
            }
        }

        private static Type TryInferBinderTypeFromViewBase(Type viewType)
        {
            var type = viewType;
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType && type.GetGenericArguments().Length == 2)
                {
                    var args = type.GetGenericArguments();
                    if (typeof(UIViewBinder).IsAssignableFrom(args[1]))
                        return args[1];
                }

                type = type.BaseType;
            }

            return null;
        }
    }

    #if UNITY_EDITOR
    // ============================================================
    // 6. Inspector
    // ============================================================

    [CustomEditor(typeof(CSharpUIBindBehaviour))]
    public sealed class CSharpUIBindBehaviourEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var bind = (CSharpUIBindBehaviour)target;

            GUILayout.Space(8);

            if (GUILayout.Button("Refresh Bindings Recursive"))
            {
                bind.RefreshBindingsInEditor(true);
            }

            if (GUILayout.Button("Refresh This Binder Only"))
            {
                bind.RefreshBindingsInEditor(false);
            }

            if (GUILayout.Button("Import Bindings From Generated Code"))
            {
                bind.ImportBindingsFromGeneratedCodeInEditor(false);
            }

            if (GUILayout.Button("Import Bindings From Generated Code Recursive"))
            {
                bind.ImportBindingsFromGeneratedCodeInEditor(true);
            }

            if (GUILayout.Button("Generate Strong Binder"))
            {
                bind.RefreshBindingsInEditor(true);
                CSharpUIBindCodeGenerator.Generate(bind);
            }

            if (GUILayout.Button("Generate AI Partial View Binding"))
            {
                bind.RefreshBindingsInEditor(true);
                AIGeneratedUIViewCodeGenerator.Generate(bind);
            }
        }
    }

    // ============================================================
    // 7. Prefab Save Auto Processor
    //    保存 Prefab 时自动刷新和生成，避免手动点按钮。
    // ============================================================

    [InitializeOnLoad]
    public static class CSharpUIBindAutoProcessor
    {
        static CSharpUIBindAutoProcessor()
        {
            PrefabStage.prefabSaving -= OnPrefabSaving;
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            if (prefabRoot == null)
                return;

            var binders = prefabRoot.GetComponentsInChildren<CSharpUIBindBehaviour>(true);
            if (binders == null || binders.Length == 0)
                return;

            // 只刷新最外层 Binder，它会递归刷新子 Binder。
            var rootBinder = prefabRoot.GetComponent<CSharpUIBindBehaviour>();
            if (rootBinder != null)
            {
                rootBinder.RefreshBindingsInEditor(true);

                if (rootBinder.AutoGenerateOnPrefabSave)
                    CSharpUIBindCodeGenerator.GenerateRecursive(rootBinder);

                if (rootBinder.AutoGenerateViewBindingsOnPrefabSave)
                    AIGeneratedUIViewCodeGenerator.GenerateRecursive(rootBinder);
            }
            else
            {
                foreach (var binder in binders)
                {
                    binder.RefreshBindingsInEditor(false);
                    if (binder.AutoGenerateOnPrefabSave)
                        CSharpUIBindCodeGenerator.Generate(binder);
                    if (binder.AutoGenerateViewBindingsOnPrefabSave)
                        AIGeneratedUIViewCodeGenerator.Generate(binder);
                }
            }
        }
    }

    // ============================================================
    // 8. Code Generator
    // ============================================================

    public static class CSharpUIBindCodeGenerator
    {
        public static void GenerateRecursive(CSharpUIBindBehaviour root)
        {
            var binders = root.GetComponentsInChildren<CSharpUIBindBehaviour>(true);
            foreach (var binder in binders)
            {
                if (binder.AutoGenerateOnPrefabSave)
                    Generate(binder);
            }
        }

        public static void Generate(CSharpUIBindBehaviour bind)
        {
            if (bind == null)
                return;

            bind.RefreshBindingsInEditor(false);

            var folder = bind.GetGeneratedCodeFolderInEditor();
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var code = BuildBinderCode(bind, bind.GeneratedNamespace, bind.BinderClassName);
            var path = Path.Combine(folder, bind.BinderClassName + ".g.cs");
            File.WriteAllText(path, code, Encoding.UTF8);
            AssetDatabase.Refresh();
            Debug.Log($"[CSharpUIBindCodeGenerator] Generated: {path}", bind);
        }

        private static string BuildBinderCode(CSharpUIBindBehaviour bind, string ns, string className)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated>");
            sb.AppendLine("// Generated by CSharpUIBindCodeGenerator. Do not edit manually.");
            sb.AppendLine("// </auto-generated>");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using TMPro;");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(ns))
            {
                sb.AppendLine($"namespace {ns}");
                sb.AppendLine("{");
            }

            var indent = string.IsNullOrWhiteSpace(ns) ? "" : "    ";
            sb.AppendLine($"{indent}public sealed class {className} : UIViewBinder");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    public {className}(CSharpUIBindBehaviour source) : base(source) {{ }}");
            sb.AppendLine();

            foreach (var item in bind.Items)
            {
                AppendItemCode(sb, indent + "    ", item);
            }

            sb.AppendLine($"{indent}}}");

            if (!string.IsNullOrWhiteSpace(ns))
                sb.AppendLine("}");

            return sb.ToString();
        }

        private static void AppendItemCode(StringBuilder sb, string indent, UIBindItem item)
        {
            var key = item.Key;
            var name = UIBindNameUtility.ToSafeIdentifier(key);

            if (item.IsSubBinder)
            {
                sb.AppendLine($"{indent}public UIObjectRef {name}Ref => Get(\"{key}\");");

                if (!string.IsNullOrWhiteSpace(item.NestedBinderTypeName))
                {
                    sb.AppendLine($"{indent}public global::{item.NestedBinderTypeName} {name} => GetBinder<global::{item.NestedBinderTypeName}>(\"{key}\");");
                }
                else
                {
                    sb.AppendLine($"{indent}public UIViewBinder {name} => GetBinder(\"{key}\");");
                }

                sb.AppendLine();
                return;
            }

            var refType = GetRefType(item);
            var getter = GetGetterMethod(item);

            sb.AppendLine($"{indent}public {refType} {name} => {getter}(\"{key}\");");

            // 额外生成组件直取属性，方便需要 Unity 原生组件时使用。
            foreach (var component in item.Components)
            {
                if (component?.Component == null)
                    continue;

                var typeName = GetTypeName(component.Component.GetType());
                var alias = UIBindNameUtility.ToSafeIdentifier(component.Alias);
                sb.AppendLine($"{indent}public {typeName} {name}_{alias} => {name}.Get<{typeName}>();");
            }

            sb.AppendLine();
        }

        private static string GetRefType(UIBindItem item)
        {
            return item.MainAlias switch
            {
                "Button" => "UIButtonRef",
                "Text" => "UITextRef",
                "TMPText" => "UITextRef",
                "Image" => "UIImageRef",
                "ScrollRect" => "UIScrollRef",
                _ => "UIObjectRef"
            };
        }

        private static string GetGetterMethod(UIBindItem item)
        {
            return item.MainAlias switch
            {
                "Button" => "Btn",
                "Text" => "Txt",
                "TMPText" => "Txt",
                "Image" => "Img",
                "ScrollRect" => "Scroll",
                _ => "Get"
            };
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(TextMeshProUGUI)) return "TextMeshProUGUI";
            if (type == typeof(TMP_InputField)) return "TMP_InputField";
            if (type == typeof(TMP_Dropdown)) return "TMP_Dropdown";
            return type.Name;
        }
    }
#endif

