using UnityEngine;
using UnityEngine.UI;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private void CreateRuntimeHud()
        {
            runtimeHud ??= new ShuttleFeedRuntimeHud(
                () =>
                {
                    inputMode = BadmintonInputMode.Sensor;
                    ActivateInputMode(inputMode);
                },
                () =>
                {
                    inputMode = BadmintonInputMode.Legacy;
                    ActivateInputMode(inputMode);
                });
        }

        private void UpdateRuntimeHud()
        {
            using (HudUpdateMarker.Auto())
            {
                runtimeHud?.Update(new RuntimeHudState(
                    screenState == ScreenState.Playing,
                    playerScore,
                    opponentScore,
                    $"{GetModeLabel()}   N{difficultyLevel}   {scoreTarget} Points",
                    opponentStamina,
                    opponentMaxStamina,
                    inputSnapshot.Status,
                    inputSnapshot.CameraStatus,
                    inputSnapshot.PhoneStatus,
                    inputSnapshot.PhoneUrl,
                    inputMode));
            }
        }

        private void DestroyRuntimeHud()
        {
            runtimeHud?.Dispose();
            runtimeHud = null;
        }
    }

    internal readonly struct RuntimeHudState
    {
        public readonly bool Visible;
        public readonly int PlayerScore;
        public readonly int OpponentScore;
        public readonly string MatchLabel;
        public readonly float OpponentStamina;
        public readonly float OpponentMaxStamina;
        public readonly string InputStatus;
        public readonly string CameraStatus;
        public readonly string PhoneStatus;
        public readonly string PhoneUrl;
        public readonly BadmintonInputMode InputMode;

        public RuntimeHudState(
            bool visible,
            int playerScore,
            int opponentScore,
            string matchLabel,
            float opponentStamina,
            float opponentMaxStamina,
            string inputStatus,
            string cameraStatus,
            string phoneStatus,
            string phoneUrl,
            BadmintonInputMode inputMode)
        {
            Visible = visible;
            PlayerScore = playerScore;
            OpponentScore = opponentScore;
            MatchLabel = matchLabel;
            OpponentStamina = opponentStamina;
            OpponentMaxStamina = opponentMaxStamina;
            InputStatus = inputStatus;
            CameraStatus = cameraStatus;
            PhoneStatus = phoneStatus;
            PhoneUrl = phoneUrl;
            InputMode = inputMode;
        }
    }

    internal sealed class ShuttleFeedRuntimeHud
    {
        private readonly GameObject root;
        private readonly Text scoreText;
        private readonly Text matchText;
        private readonly Text staminaText;
        private readonly Image staminaFill;
        private readonly Text statusText;
        private readonly Text cameraText;
        private readonly Text phoneText;
        private readonly Text phoneUrlText;
        private readonly Button sensorButton;
        private readonly Button legacyButton;
        private string lastScore;
        private string lastMatch;
        private string lastStamina;
        private string lastStatus;
        private string lastCamera;
        private string lastPhone;
        private string lastPhoneUrl;

        public ShuttleFeedRuntimeHud(
            UnityEngine.Events.UnityAction switchToSensor,
            UnityEngine.Events.UnityAction switchToLegacy)
        {
            root = new GameObject("VR Badminton Runtime HUD");
            Object.DontDestroyOnLoad(root);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            Font font = LoadRuntimeFont();
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            RectTransform scorePanel = CreatePanel(
                "Score",
                rootRect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(260f, 72f),
                new Vector2(0f, -18f));
            scoreText = CreateText(
                "Score Text",
                scorePanel,
                font,
                34,
                TextAnchor.MiddleCenter,
                new Vector2(1f, 0.6f),
                Vector2.zero,
                Color.white);
            matchText = CreateText(
                "Match Text",
                scorePanel,
                font,
                14,
                TextAnchor.MiddleCenter,
                new Vector2(1f, 0.4f),
                new Vector2(0f, -18f),
                new Color(0.82f, 0.88f, 0.92f, 1f));

            RectTransform staminaPanel = CreatePanel(
                "Opponent Stamina",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(286f, 74f),
                new Vector2(18f, -18f));
            staminaText = CreateText(
                "Stamina Text",
                staminaPanel,
                font,
                14,
                TextAnchor.UpperLeft,
                new Vector2(1f, 1f),
                new Vector2(12f, -8f),
                Color.white);
            Image staminaTrack = CreateImage(
                "Stamina Track",
                staminaPanel,
                new Color(0.16f, 0.18f, 0.2f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(12f, 12f),
                new Vector2(-12f, 28f));
            staminaFill = CreateImage(
                "Stamina Fill",
                staminaTrack.rectTransform,
                new Color(0.15f, 0.78f, 0.95f, 1f),
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Vector2.zero,
                Vector2.zero);
            staminaFill.rectTransform.pivot = new Vector2(0f, 0.5f);

            RectTransform statusPanel = CreatePanel(
                "Input Status",
                rootRect,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(374f, 188f),
                new Vector2(18f, -104f));
            sensorButton = CreateButton(
                "Sensor Button",
                statusPanel,
                font,
                "Sensor",
                new Vector2(12f, -12f),
                new Vector2(166f, 32f),
                switchToSensor);
            legacyButton = CreateButton(
                "Legacy Button",
                statusPanel,
                font,
                "Legacy",
                new Vector2(196f, -12f),
                new Vector2(166f, 32f),
                switchToLegacy);
            statusText = CreateText(
                "Status Text",
                statusPanel,
                font,
                13,
                TextAnchor.UpperLeft,
                new Vector2(1f, 0f),
                new Vector2(12f, -52f),
                Color.white);
            cameraText = CreateText(
                "Camera Text",
                statusPanel,
                font,
                13,
                TextAnchor.UpperLeft,
                new Vector2(1f, 0f),
                new Vector2(12f, -78f),
                Color.white);
            phoneText = CreateText(
                "Phone Text",
                statusPanel,
                font,
                13,
                TextAnchor.UpperLeft,
                new Vector2(1f, 0f),
                new Vector2(12f, -104f),
                Color.white);
            phoneUrlText = CreateText(
                "Phone URL Text",
                statusPanel,
                font,
                12,
                TextAnchor.UpperLeft,
                new Vector2(1f, 0f),
                new Vector2(12f, -130f),
                new Color(0.82f, 0.88f, 0.92f, 1f));
            phoneUrlText.horizontalOverflow = HorizontalWrapMode.Wrap;
            phoneUrlText.verticalOverflow = VerticalWrapMode.Overflow;

            root.SetActive(false);
        }

        public void Update(RuntimeHudState state)
        {
            if (root.activeSelf != state.Visible)
            {
                root.SetActive(state.Visible);
            }

            if (!state.Visible)
            {
                return;
            }

            SetText(scoreText, ref lastScore, $"{state.PlayerScore}  :  {state.OpponentScore}");
            SetText(matchText, ref lastMatch, state.MatchLabel);

            float staminaRatio = state.OpponentMaxStamina <= 0f
                ? 0f
                : Mathf.Clamp01(state.OpponentStamina / state.OpponentMaxStamina);
            Vector2 fillMax = staminaFill.rectTransform.anchorMax;
            fillMax.x = staminaRatio;
            staminaFill.rectTransform.anchorMax = fillMax;
            SetText(
                staminaText,
                ref lastStamina,
                $"Opponent Stamina  {Mathf.CeilToInt(state.OpponentStamina)}/{Mathf.CeilToInt(state.OpponentMaxStamina)}");

            SetText(statusText, ref lastStatus, state.InputStatus);
            SetText(cameraText, ref lastCamera, $"Camera: {state.CameraStatus}");
            SetText(phoneText, ref lastPhone, $"Phone: {state.PhoneStatus}");
            SetText(
                phoneUrlText,
                ref lastPhoneUrl,
                string.IsNullOrEmpty(state.PhoneUrl)
                    ? "Phone URL: unavailable"
                    : $"Phone URL: {state.PhoneUrl}");
            SetButtonSelected(sensorButton, state.InputMode == BadmintonInputMode.Sensor);
            SetButtonSelected(legacyButton, state.InputMode == BadmintonInputMode.Legacy);
        }

        public void Dispose()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        private static RectTransform CreatePanel(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            GameObject panel = new GameObject(name);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.03f, 0.04f, 0.05f, 0.82f);
            return rect;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            Font font,
            int fontSize,
            TextAnchor alignment,
            Vector2 stretch,
            Vector2 offset,
            Color color)
        {
            GameObject textObject = new GameObject(name);
            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f - stretch.y);
            rect.anchorMax = new Vector2(stretch.x, 1f);
            rect.offsetMin = new Vector2(offset.x, offset.y - 24f);
            rect.offsetMax = new Vector2(-offset.x, offset.y);

            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image CreateImage(
            string name,
            RectTransform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            GameObject imageObject = new GameObject(name);
            RectTransform rect = imageObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Font LoadRuntimeFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : GUI.skin.font;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            Font font,
            string label,
            Vector2 anchoredPosition,
            Vector2 size,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject = new GameObject(name);
            RectTransform rect = buttonObject.AddComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image background = buttonObject.AddComponent<Image>();
            background.color = Color.white;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(onClick);

            Text text = CreateText(
                "Label",
                rect,
                font,
                14,
                TextAnchor.MiddleCenter,
                Vector2.one,
                Vector2.zero,
                new Color(0.04f, 0.05f, 0.06f, 1f));
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return button;
        }

        private static void SetText(Text text, ref string lastValue, string value)
        {
            if (lastValue == value)
            {
                return;
            }

            lastValue = value;
            text.text = value;
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            Image image = button.targetGraphic as Image;
            if (image == null)
            {
                return;
            }

            image.color = selected
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
        }
    }
}
