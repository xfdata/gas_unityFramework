# UI Binding Rules

## Preferred Binding Path

For new generated UGUI Views, prefer `CSharpUIBindBehaviour` + `UIBindNode` + generated binder.

Root `CSharpUIBindBehaviour` settings:

```text
ExportToParent = true
ParentBindName = XxxView
AutoGenerateOnPrefabSave = true
GeneratedNamespace = Game.UI.Generated
GeneratedClassName = XxxViewBinder
GeneratedFolder = Assets/Scripts/HotUpdate.Core/Gameplay/Views (or the feature's View folder)
AutoGenerateViewBindingsOnPrefabSave = false unless using generated partial View fields
GeneratedViewClassName = XxxView
```

For each exported node, add `UIBindNode` and set:

- `BindName` to the exact runtime key.
- `Export = true`.
- `IsSubBinder = true` only when the node owns a child `CSharpUIBindBehaviour`.
- `NestedBinderTypeName` only when a strong nested binder type is required.

After building the hierarchy in an Editor script:

```csharp
var bind = root.GetComponent<CSharpUIBindBehaviour>();
bind.RefreshBindingsInEditor(true);
PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
```

## Access Patterns

Use one of these styles:

```csharp
private UIButtonRef _btnSend;
private TMP_InputField _inputMessage;

private void CacheBindings()
{
    Cache(ref _btnSend, "btn_Send");
    Cache(ref _inputMessage, "input_Message");
}
```

```csharp
BindClick(_btnSend.Button, OnSendClicked);
```

```csharp
protected override UniTask OnClose(object result)
{
    _btnSend.Button.onClick.RemoveListener(OnSendClicked);
    return UniTask.CompletedTask;
}
```

Use `BindClick` when possible. If using `onClick.AddListener` directly, remove the listener in `OnClose` or `OnStop`.

## Compatibility Path

Existing `LoginView` and `LoadingView` show `[UI]` fields:

```csharp
[UI] private TextMeshProUGUI txt_BundleVersion;
[UI] private TMP_InputField input_Account;
[UI] private Button btn_Login;
```

This works because `UIViewAutoBind` guesses a node name from the field name, strips known suffixes, and searches the prefab hierarchy. It is allowed for small or legacy Views, but new generated UI should prefer explicit binder nodes to avoid runtime hierarchy searches.

If using `[UI]`, ensure the field name exactly matches a node name or provide an explicit path:

```csharp
[UI("SafeArea/Panel_InputBar/input_Message")]
private TMP_InputField input_Message;
```

## Naming

Use binding-safe names. Choose the local style and keep it consistent:

- HotUpdate style: `btn_Send`, `txt_Title`, `img_Backdrop`, `input_Message`, `scroll_Items`, `Panel_InputBar`.
- Binder-heavy style: `BtnSend`, `TxtTitle`, `ImgBackdrop`, `InputMessage`, `ScrollItems`, `PanelInputBar`.

Do not use spaces, punctuation, or localized text in bind keys.

## Generated Binder Notes

- `CSharpUIBindCodeGenerator` emits `XxxViewBinder.g.cs`.
- `AIGeneratedUIViewCodeGenerator` can emit partial View fields that implement `IUIViewGeneratedBinding`.
- Generated files are `.g.cs`; do not manually edit them.
- If the binder file exists but has no properties, refresh bindings on the prefab and regenerate. Empty `_items` means no exported `UIBindNode` was collected.

## Runtime Find Ban

Forbidden in runtime View/module code:

```csharp
GameObject.Find("...");
transform.Find("...") // repeated runtime lookup
GetComponentsInChildren<T>() // runtime UI refresh path
```

Allowed in Editor generator scripts and existing binding processors:

```csharp
root.transform.Find("..."); // editor validation/generation only
GetComponentsInChildren<UIBindNode>(true); // editor binding refresh only
```
