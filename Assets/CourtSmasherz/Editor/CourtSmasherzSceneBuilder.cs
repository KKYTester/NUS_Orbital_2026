using CourtSmasherz;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CourtSmasherzSceneBuilder
{
    [MenuItem("Court Smasherz/Build 3D Prototype Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "CourtSmasherz3D";

        Material courtMat = MakeMaterial("Court Green", new Color(0.08f, 0.42f, 0.35f));
        Material lineMat = MakeMaterial("Court Lines", new Color(0.95f, 0.92f, 0.82f));
        Material netMat = MakeMaterial("Net Dark", new Color(0.05f, 0.07f, 0.08f));
        Material p1Mat = MakeMaterial("P1 Gold", new Color(0.95f, 0.76f, 0.25f));
        Material p2Mat = MakeMaterial("P2 Blue", new Color(0.35f, 0.75f, 1f));
        Material skinMat = MakeMaterial("Avatar Skin", new Color(0.94f, 0.72f, 0.52f));
        Material racquetMat = MakeMaterial("Racquet Red", new Color(0.88f, 0.15f, 0.12f));
        Material ballMat = MakeMaterial("Pickleball Yellow", new Color(0.95f, 0.9f, 0.2f));

        GameObject root = new GameObject("Court Smasherz 3D Prototype");
        CreateCube("Court Surface", root.transform, Vector3.zero, new Vector3(18f, 0.15f, 10f), courtMat);
        CreateCube("Center Net", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.15f, 1.1f, 10f), netMat);

        CreateCube("Outer Line North", root.transform, new Vector3(0f, 0.12f, 5f), new Vector3(18.2f, 0.04f, 0.08f), lineMat);
        CreateCube("Outer Line South", root.transform, new Vector3(0f, 0.12f, -5f), new Vector3(18.2f, 0.04f, 0.08f), lineMat);
        CreateCube("Outer Line West", root.transform, new Vector3(-9f, 0.12f, 0f), new Vector3(0.08f, 0.04f, 10f), lineMat);
        CreateCube("Outer Line East", root.transform, new Vector3(9f, 0.12f, 0f), new Vector3(0.08f, 0.04f, 10f), lineMat);
        CreateCube("Center Line", root.transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.08f, 0.04f, 10f), lineMat);
        CreateCube("Kitchen Line P1", root.transform, new Vector3(-2.2f, 0.13f, 0f), new Vector3(0.08f, 0.04f, 10f), lineMat);
        CreateCube("Kitchen Line P2", root.transform, new Vector3(2.2f, 0.13f, 0f), new Vector3(0.08f, 0.04f, 10f), lineMat);
        CreateCube("Service Split", root.transform, new Vector3(0f, 0.13f, 0f), new Vector3(18f, 0.04f, 0.07f), lineMat);

        PlayerRig p1 = CreatePlayerRig("P1 Avatar", root.transform, new Vector3(-7.3f, 0f, 0f), 90f, p1Mat, skinMat, racquetMat);
        PlayerRig p2 = CreatePlayerRig("P2 Avatar", root.transform, new Vector3(7.3f, 0f, 0f), -90f, p2Mat, skinMat, racquetMat);
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Pickleball";
        ball.transform.SetParent(root.transform);
        ball.transform.position = new Vector3(0f, 0.55f, 0f);
        ball.transform.localScale = Vector3.one * 0.42f;
        ball.GetComponent<Renderer>().sharedMaterial = ballMat;

        GameObject manager = new GameObject("Game Manager");
        CourtSmasherzGameManager game = manager.AddComponent<CourtSmasherzGameManager>();
        PhoneMotionHttpBridge bridge = manager.AddComponent<PhoneMotionHttpBridge>();
        manager.AddComponent<KeyboardShotTester>();
        game.ball = ball.transform;
        game.playerOneRoot = p1.root.transform;
        game.playerTwoRoot = p2.root.transform;
        game.playerOneRacquet = p1.racquet.transform;
        game.playerTwoRacquet = p2.racquet.transform;
        bridge.gameManager = game;

        CreateSplitScreenCameras(p1.root.transform, p2.root.transform, ball.transform);
        CreateLighting();
        CreateHud(game, bridge);
        CreateEventSystem();

        string scenePath = "Assets/CourtSmasherz/Scenes/CourtSmasherz3D.unity";
        System.IO.Directory.CreateDirectory("Assets/CourtSmasherz/Scenes");
        EditorSceneManager.SaveScene(scene, scenePath);
        Selection.activeGameObject = manager;
        Debug.Log($"Court Smasherz 3D scene built at {scenePath}");
    }

    private struct PlayerRig
    {
        public GameObject root;
        public GameObject racquet;
    }

    private static PlayerRig CreatePlayerRig(
        string name,
        Transform parent,
        Vector3 position,
        float yRotation,
        Material outfitMaterial,
        Material skinMaterial,
        Material racquetMaterial)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        CreateLocalCube("Body", root.transform, new Vector3(0f, 0.95f, 0f), new Vector3(0.72f, 1.25f, 0.42f), outfitMaterial);
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.82f, 0f);
        head.transform.localScale = Vector3.one * 0.48f;
        head.GetComponent<Renderer>().sharedMaterial = skinMaterial;

        CreateLocalCube("Left Arm", root.transform, new Vector3(-0.55f, 1.05f, 0f), new Vector3(0.22f, 0.9f, 0.22f), skinMaterial);
        GameObject rightArm = CreateLocalCube("Racquet Arm", root.transform, new Vector3(0.55f, 1.05f, 0.16f), new Vector3(0.22f, 0.9f, 0.22f), skinMaterial);
        rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);

        GameObject racquetPivot = new GameObject("Racquet Pivot");
        racquetPivot.transform.SetParent(root.transform, false);
        racquetPivot.transform.localPosition = new Vector3(0.78f, 1.05f, 0.42f);
        racquetPivot.transform.localRotation = Quaternion.Euler(12f, 18f, -20f);

        GameObject handle = CreateLocalCube("Racquet Handle", racquetPivot.transform, new Vector3(0f, -0.32f, 0f), new Vector3(0.08f, 0.65f, 0.08f), racquetMaterial);
        GameObject face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        face.name = "Racquet Face";
        face.transform.SetParent(racquetPivot.transform, false);
        face.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        face.transform.localScale = new Vector3(0.34f, 0.035f, 0.46f);
        face.GetComponent<Renderer>().sharedMaterial = racquetMaterial;

        CreateLocalCube("Left Leg", root.transform, new Vector3(-0.22f, 0.28f, 0f), new Vector3(0.22f, 0.58f, 0.22f), outfitMaterial);
        CreateLocalCube("Right Leg", root.transform, new Vector3(0.22f, 0.28f, 0f), new Vector3(0.22f, 0.58f, 0.22f), outfitMaterial);

        return new PlayerRig
        {
            root = root,
            racquet = racquetPivot
        };
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static GameObject CreateLocalCube(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (material.shader == null)
        {
            material = new Material(Shader.Find("Standard"));
        }

        material.name = name;
        material.color = color;
        return material;
    }

    private static void CreateSplitScreenCameras(Transform p1, Transform p2, Transform ball)
    {
        CreatePlayerCamera("P1 Camera", p1, ball, new Rect(0f, 0f, 0.5f, 1f), new Vector3(-1.75f, 4.35f, -6.45f));
        CreatePlayerCamera("P2 Camera", p2, ball, new Rect(0.5f, 0f, 0.5f, 1f), new Vector3(1.75f, 4.35f, -6.45f));
    }

    private static void CreatePlayerCamera(string name, Transform followTarget, Transform lookTarget, Rect viewport, Vector3 offset)
    {
        GameObject cameraObject = new GameObject(name);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.rect = viewport;
        camera.fieldOfView = 60f;
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.07f, 0.07f);
        cameraObject.transform.position = followTarget.TransformPoint(offset);
        cameraObject.transform.LookAt(lookTarget.position + Vector3.up * 0.4f);

        SplitScreenFollowCamera follow = cameraObject.AddComponent<SplitScreenFollowCamera>();
        follow.followTarget = followTarget;
        follow.lookTarget = lookTarget;
        follow.localOffset = offset;
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2.6f;
        lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 20f);
        RenderSettings.ambientLight = new Color(0.45f, 0.5f, 0.52f);
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private static void CreateHud(CourtSmasherzGameManager game, PhoneMotionHttpBridge bridge)
    {
        GameObject canvasObject = new GameObject("HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        Text score = CreateText("Score Text", canvasObject.transform, new Vector2(0f, -32f), 34, TextAnchor.UpperCenter);
        Text status = CreateBottomText("Status Text", canvasObject.transform, new Vector2(0f, 36f), 18, TextAnchor.LowerCenter);
        Text room = CreateText("Room Code Text", canvasObject.transform, new Vector2(0f, -112f), 22, TextAnchor.UpperCenter);
        Text bridgeStatus = CreateText("Bridge Status Text", canvasObject.transform, new Vector2(0f, -146f), 16, TextAnchor.UpperCenter);
        Text p1Motion = CreatePlayerMotionText("P1 Motion Status Text", canvasObject.transform, new Vector2(0.25f, 0.18f), new Vector2(0.5f, 0.18f), TextAnchor.MiddleCenter, new Color(1f, 0.86f, 0.38f));
        Text p2Motion = CreatePlayerMotionText("P2 Motion Status Text", canvasObject.transform, new Vector2(0.75f, 0.18f), new Vector2(0.5f, 0.18f), TextAnchor.MiddleCenter, new Color(0.48f, 0.84f, 1f));
        game.scoreText = score;
        game.statusText = status;
        bridge.roomCodeText = room;
        bridge.bridgeStatusText = bridgeStatus;
        bridge.playerOneMotionStatusText = p1Motion;
        bridge.playerTwoMotionStatusText = p2Motion;

        MainMenuController menu = CreateMainMenu(canvasObject.transform, bridge);
        bridge.mainMenu = menu;
    }

    private static MainMenuController CreateMainMenu(Transform canvasRoot, PhoneMotionHttpBridge bridge)
    {
        GameObject panelObject = new GameObject("Main Menu");
        panelObject.transform.SetParent(canvasRoot, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.02f, 0.07f, 0.06f, 0.9f);
        CanvasGroup group = panelObject.AddComponent<CanvasGroup>();

        Text title = CreateMenuText("Title", panelObject.transform, new Vector2(0f, 222f), new Vector2(760f, 70f), 46, TextAnchor.MiddleCenter);
        title.text = "Court Smasherz";
        title.fontStyle = FontStyle.Bold;
        title.color = new Color(1f, 0.86f, 0.38f);

        RawImage qrImage = CreateQrImage(panelObject.transform, new Vector2(0f, 62f), new Vector2(280f, 280f));

        Text room = CreateMenuText("Menu Room Text", panelObject.transform, new Vector2(0f, -102f), new Vector2(760f, 44f), 26, TextAnchor.MiddleCenter);
        room.text = "Room: creating...";
        room.fontStyle = FontStyle.Bold;

        Text url = CreateMenuText("Menu Phone URL Text", panelObject.transform, new Vector2(0f, -148f), new Vector2(920f, 38f), 18, TextAnchor.MiddleCenter);
        url.text = "Phone URL: starting server...";
        url.color = new Color(0.83f, 0.94f, 0.9f);

        Button startButton = CreateButton("Start Button", panelObject.transform, new Vector2(0f, -208f), new Vector2(220f, 58f), "Start");

        Text ready = CreateMenuText("Ready Status Text", panelObject.transform, new Vector2(0f, -274f), new Vector2(760f, 44f), 24, TextAnchor.MiddleCenter);
        ready.text = "Join both phones, then press Start.";

        MainMenuController menu = panelObject.AddComponent<MainMenuController>();
        menu.menuGroup = group;
        menu.qrImage = qrImage;
        menu.roomCodeText = room;
        menu.phoneUrlText = url;
        menu.readyStatusText = ready;
        menu.startButton = startButton;
        menu.bridge = bridge;
        return menu;
    }

    private static RawImage CreateQrImage(Transform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject imageObject = new GameObject("QR Code Image");
        imageObject.transform.SetParent(parent, false);
        RawImage image = imageObject.AddComponent<RawImage>();
        image.color = Color.white;
        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return image;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, string label)
    {
        GameObject buttonObject = new GameObject(name);
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.95f, 0.76f, 0.25f);
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = CreateMenuText("Label", buttonObject.transform, Vector2.zero, size, 24, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = new Color(0.02f, 0.07f, 0.06f);
        text.fontStyle = FontStyle.Bold;
        return button;
    }

    private static Text CreateMenuText(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static Text CreateText(string name, Transform parent, Vector2 anchoredPosition, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(0f, 50f);
        return text;
    }

    private static Text CreateBottomText(string name, Transform parent, Vector2 anchoredPosition, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(0f, 48f);
        return text;
    }

    private static Text CreatePlayerMotionText(string name, Transform parent, Vector2 anchor, Vector2 anchorSize, TextAnchor alignment, Color color)
    {
        GameObject textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        text.gameObject.SetActive(false);

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(anchor.x - anchorSize.x * 0.5f, anchor.y - anchorSize.y * 0.5f);
        rect.anchorMax = new Vector2(anchor.x + anchorSize.x * 0.5f, anchor.y + anchorSize.y * 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-40f, -20f);
        return text;
    }
}
