using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using VRBadminton.Gameplay;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private void OnGUI()
        {
            using (GuiMarker.Auto())
            {
                EnsureGuiStyles();
                if (screenState != ScreenState.Playing)
                {
                    DrawFrontend();
                    return;
                }

                const float barWidth = 34f;
                float barHeight = Mathf.Min(360f, Screen.height * 0.58f);
                float x = Screen.width - 78f;
                float y = (Screen.height - barHeight) * 0.5f;

                Color previousColor = GUI.color;
                GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.82f);
                GUI.Box(new Rect(x - 14f, y - 36f, barWidth + 28f, barHeight + 72f), GUIContent.none);

                GUI.color = new Color(0.16f, 0.18f, 0.2f, 1f);
                GUI.DrawTexture(new Rect(x, y, barWidth, barHeight), Texture2D.whiteTexture);

                float powerHeight = barHeight * displayedPower;
                GUI.color = Color.Lerp(
                    new Color(0.25f, 0.85f, 0.35f, 1f),
                    new Color(1f, 0.25f, 0.08f, 1f),
                    displayedPower);
                GUI.DrawTexture(
                    new Rect(x + 5f, y + barHeight - powerHeight, barWidth - 10f, powerHeight),
                    Texture2D.whiteTexture);

                float indicatorY = y + (1f - currentMouseY) * barHeight;
                GUI.color = new Color(1f, 0.82f, 0.08f, 1f);
                GUI.DrawTexture(new Rect(x - 7f, indicatorY - 3f, barWidth + 14f, 6f), Texture2D.whiteTexture);

                GUI.color = Color.white;
                uiLabelStyle ??= new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(x - 24f, y - 30f, barWidth + 48f, 24f), "立拍", uiLabelStyle);
                GUI.Label(new Rect(x - 24f, y + barHeight + 6f, barWidth + 48f, 24f), "平拍", uiLabelStyle);
                GUI.Label(
                    new Rect(x - 44f, y + barHeight + 30f, barWidth + 88f, 24f),
                    inputMode == BadmintonInputMode.Sensor
                        ? "Sensor racket"
                        : isBackhand ? "Backhand [Q]" : "Forehand [Q]",
                    uiLabelStyle);

                if (!isPaused)
                {
                    DrawPauseButton();
                }

                DrawCameraPreview();
                DrawHitDebug();

                if (incomingOpponentSmash)
                {
                    GUI.color = smashReceiveReady
                        ? new Color(0.25f, 1f, 0.4f, 1f)
                        : new Color(1f, 0.35f, 0.18f, 1f);
                    GUI.Label(
                        new Rect(Screen.width * 0.5f - 130f, 24f, 260f, 30f),
                        smashReceiveReady
                            ? "READY - SWING UP"
                            : inputMode == BadmintonInputMode.Sensor
                                ? "RAISE HAND / PREPARE SWING"
                                : "PRESS SPACE TO RECEIVE",
                        uiLabelStyle);
                }

                if (awaitingOpponentServe)
                {
                    GUI.color = new Color(1f, 0.86f, 0.25f, 1f);
                    GUI.Label(
                        new Rect(Screen.width * 0.5f - 180f, 62f, 360f, 30f),
                        "PRESS SPACE TO START SERVE",
                        uiLabelStyle);
                }

                if (matchOver)
                {
                    DrawMatchEnd();
                }

                DrawTemporarySlowMotionToggle();

                if (awaitingPlayerServe)
                {
                    GUI.color = new Color(1f, 0.86f, 0.25f, 1f);
                    GUI.Label(
                        new Rect(Screen.width * 0.5f - 150f, 62f, 300f, 30f),
                        "YOUR SERVE - SWING UP",
                        uiLabelStyle);
                }

                if (isPaused)
                {
                    DrawPauseMenu();
                    if (settingsOpen)
                    {
                        DrawSettings(false);
                    }
                }

                GUI.color = previousColor;
            }
        }

        private void DrawMatchEnd()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.Box(
                new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - 80f, 380f, 160f),
                GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(Screen.width * 0.5f - 170f, Screen.height * 0.5f - 60f, 340f, 42f),
                matchWinner == 1 ? "YOU WIN" : "OPPONENT WINS",
                new GUIStyle(uiLabelStyle) { fontSize = 24 });
            if (GUI.Button(
                new Rect(Screen.width * 0.5f - 135f, Screen.height * 0.5f - 5f, 270f, 36f),
                "Restart Same Settings",
                buttonStyle))
            {
                RestartMatch();
            }
            if (GUI.Button(
                new Rect(Screen.width * 0.5f - 135f, Screen.height * 0.5f + 40f, 270f, 32f),
                "Main Menu",
                buttonStyle))
            {
                ReturnToMainMenu();
            }
        }

        private void DrawSettings(bool showToggle = true)
        {
            if (showToggle && !settingsOpen)
            {
                return;
            }

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.88f);
            float panelHeight = resolutionOptionsOpen ? 286f : 152f;
            GUI.Box(new Rect(Screen.width - 232f, 56f, 214f, panelHeight), GUIContent.none);
            GUI.color = Color.white;

            bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
            if (GUI.Button(
                new Rect(Screen.width - 218f, 70f, 186f, 28f),
                fullscreen ? "Fullscreen: ON" : "Fullscreen: OFF",
                buttonStyle))
            {
                if (fullscreen)
                {
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    Screen.fullScreen = false;
                }
                else
                {
                    Resolution nativeResolution = Screen.currentResolution;
                    Screen.SetResolution(
                        nativeResolution.width,
                        nativeResolution.height,
                        FullScreenMode.FullScreenWindow,
                        nativeResolution.refreshRateRatio);
                    resolutionOptionsOpen = false;
                }
            }

            if (GUI.Button(
                new Rect(Screen.width - 218f, 106f, 186f, 28f),
                $"Resolution: {Screen.width} x {Screen.height}",
                buttonStyle))
            {
                resolutionOptionsOpen = !resolutionOptionsOpen;
            }

            float guideY = 142f;
            if (resolutionOptionsOpen)
            {
                string[] labels =
                {
                    "1280 x 720",
                    "1600 x 900",
                    "1920 x 1080",
                    "2560 x 1440 (2K)"
                };
                Vector2Int[] sizes =
                {
                    new Vector2Int(1280, 720),
                    new Vector2Int(1600, 900),
                    new Vector2Int(1920, 1080),
                    new Vector2Int(2560, 1440)
                };

                for (int i = 0; i < labels.Length; i++)
                {
                    float y = 142f + i * 30f;
                    if (GUI.Button(
                        new Rect(Screen.width - 218f, y, 186f, 25f),
                        labels[i],
                        buttonStyle))
                    {
                        SetWindowResolution(sizes[i].x, sizes[i].y);
                        resolutionOptionsOpen = false;
                    }
                }

                guideY = 266f;
            }

            if (GUI.Button(
                new Rect(Screen.width - 218f, guideY, 186f, 28f),
                showRacketCenterGuide ? "Guide Line: ON" : "Guide Line: OFF",
                buttonStyle))
            {
                showRacketCenterGuide = !showRacketCenterGuide;
            }
        }

        private void DrawInputStatus()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            GUIStyle statusStyle = new GUIStyle(uiLabelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                wordWrap = true
            };

            float panelX = 18f;
            float panelY = 204f;
            float panelWidth = 328f;
            float panelHeight = 154f;
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.86f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);

            Color previous = GUI.color;
            GUI.color = inputMode == BadmintonInputMode.Sensor
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(panelX + 12f, panelY + 10f, 144f, 28f), "Sensor", buttonStyle))
            {
                inputMode = BadmintonInputMode.Sensor;
                ActivateInputMode(inputMode);
            }

            GUI.color = inputMode == BadmintonInputMode.Legacy
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(panelX + 172f, panelY + 10f, 144f, 28f), "Legacy", buttonStyle))
            {
                inputMode = BadmintonInputMode.Legacy;
                ActivateInputMode(inputMode);
            }

            GUI.color = previous;
            GUI.Label(
                new Rect(panelX + 12f, panelY + 46f, panelWidth - 24f, 20f),
                inputSnapshot.Status,
                statusStyle);
            GUI.Label(
                new Rect(panelX + 12f, panelY + 68f, panelWidth - 24f, 20f),
                $"Camera: {inputSnapshot.CameraStatus}",
                statusStyle);
            GUI.Label(
                new Rect(panelX + 12f, panelY + 90f, panelWidth - 24f, 20f),
                $"Phone: {inputSnapshot.PhoneStatus}",
                statusStyle);
            GUI.Label(
                new Rect(panelX + 12f, panelY + 112f, panelWidth - 24f, 34f),
                string.IsNullOrEmpty(inputSnapshot.PhoneUrl)
                    ? "Phone URL: unavailable"
                    : $"Phone URL: {inputSnapshot.PhoneUrl}",
                statusStyle);
        }

        private void DrawCameraPreview()
        {
            if (inputMode != BadmintonInputMode.Sensor)
            {
                return;
            }

            const float margin = 18f;
            float panelWidth = Mathf.Clamp(Screen.width * 0.24f, 220f, 360f);
            panelWidth = Mathf.Min(panelWidth, Screen.width - margin * 2f);
            float panelHeight = panelWidth * 9f / 16f;
            float panelY = Screen.height - panelHeight - margin;

            if (panelY < 372f)
            {
                panelHeight = Mathf.Min(panelHeight, Mathf.Max(96f, Screen.height - 372f - margin));
                panelWidth = panelHeight * 16f / 9f;
                panelY = Screen.height - panelHeight - margin;
            }

            Rect panelRect = new Rect(margin, panelY, panelWidth, panelHeight);
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;

            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.88f);
            GUI.DrawTexture(panelRect, Texture2D.whiteTexture);

            Texture preview = inputSnapshot.CameraPreviewTexture;
            if (preview == null || preview.width <= 16 || preview.height <= 16)
            {
                GUI.color = new Color(0.82f, 0.86f, 0.9f, 0.82f);
                GUI.Label(
                    new Rect(panelRect.x + 10f, panelRect.y + panelRect.height * 0.5f - 10f, panelRect.width - 20f, 20f),
                    "Camera warming up",
                    uiLabelStyle);
                GUI.color = previousColor;
                GUI.matrix = previousMatrix;
                return;
            }

            Rect imageRect = FitRect(panelRect, preview.width / Mathf.Max(1f, (float)preview.height));
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(
                imageRect,
                preview,
                PreviewTexCoords(inputSnapshot.CameraPreviewFlipHorizontally),
                true);

            DrawPosePreviewSkeleton(
                imageRect,
                inputSnapshot.CameraPreviewLandmarks,
                inputSnapshot.CameraPreviewFlipHorizontally);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawHitDebug()
        {
            if (!showHitDebug)
            {
                return;
            }

            GUIStyle debugStyle = new GUIStyle(uiLabelStyle)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 12,
                wordWrap = true
            };
            float panelWidth = 300f;
            float panelHeight = 112f;
            float panelX = Screen.width - panelWidth - 18f;
            float panelY = 18f;
            Color previous = GUI.color;
            GUI.color = new Color(0.03f, 0.04f, 0.05f, 0.84f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(panelX + 12f, panelY + 10f, panelWidth - 24f, panelHeight - 20f),
                $"Hit: {(lastHitResult.Hit ? lastHitResult.Shot.ToString() : "Miss")}  {lastHitResult.Reason}\n" +
                $"Q {lastHitResult.Quality:0.00}  S {lastHitResult.SpatialQuality:0.00}  T {lastHitResult.TimingQuality:0.00}  D {lastHitResult.DirectionQuality:0.00}\n" +
                $"Face {lastHitResult.FaceQuality:0.00}  Power {lastHitResult.PowerQuality:0.00}  Assist {lastHitResult.AssistUsed}  Magnet {lastHitResult.MagnetUsed}",
                debugStyle);
            GUI.color = previous;
        }

        private void DrawPosePreviewSkeleton(
            Rect imageRect,
            BadmintonPoseLandmark[] landmarks,
            bool flipHorizontally)
        {
            if (!inputSnapshot.CameraPreviewPoseVisible ||
                landmarks == null ||
                landmarks.Length < 33)
            {
                return;
            }

            for (int i = 0; i < PosePreviewBonePairs.Length; i += 2)
            {
                int from = PosePreviewBonePairs[i];
                int to = PosePreviewBonePairs[i + 1];
                if (!IsPreviewLandmarkVisible(landmarks, from) || !IsPreviewLandmarkVisible(landmarks, to))
                {
                    continue;
                }

                DrawGuiLine(
                    LandmarkToPreviewPoint(landmarks[from], imageRect, flipHorizontally),
                    LandmarkToPreviewPoint(landmarks[to], imageRect, flipHorizontally),
                    new Color(0.15f, 1f, 0.88f, 0.92f),
                    2f);
            }

            for (int i = 0; i < landmarks.Length; i++)
            {
                if (!IsPreviewLandmarkVisible(landmarks, i))
                {
                    continue;
                }

                Vector2 point = LandmarkToPreviewPoint(landmarks[i], imageRect, flipHorizontally);
                float size = i == BadmintonPoseLandmarkMapper.RightWrist ||
                             i == BadmintonPoseLandmarkMapper.RightIndex ||
                             i == BadmintonPoseLandmarkMapper.RightPinky ||
                             i == BadmintonPoseLandmarkMapper.RightThumb
                    ? 5f
                    : 3.5f;
                GUI.color = i == BadmintonPoseLandmarkMapper.RightWrist ||
                            i == BadmintonPoseLandmarkMapper.RightIndex ||
                            i == BadmintonPoseLandmarkMapper.RightPinky ||
                            i == BadmintonPoseLandmarkMapper.RightThumb
                    ? new Color(1f, 0.82f, 0.2f, 0.96f)
                    : new Color(1f, 1f, 1f, 0.92f);
                GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            }
        }

        private static Rect FitRect(Rect bounds, float aspect)
        {
            float safeAspect = Mathf.Max(0.01f, aspect);
            float boundsAspect = bounds.width / Mathf.Max(1f, bounds.height);
            if (safeAspect > boundsAspect)
            {
                float height = bounds.width / safeAspect;
                return new Rect(bounds.x, bounds.y + (bounds.height - height) * 0.5f, bounds.width, height);
            }

            float width = bounds.height * safeAspect;
            return new Rect(bounds.x + (bounds.width - width) * 0.5f, bounds.y, width, bounds.height);
        }

        private static bool IsPreviewLandmarkVisible(BadmintonPoseLandmark[] landmarks, int index)
        {
            return index >= 0 &&
                   index < landmarks.Length &&
                   landmarks[index].Visibility >= 0.25f;
        }

        private static Vector2 LandmarkToPreviewPoint(
            BadmintonPoseLandmark landmark,
            Rect imageRect,
            bool flipHorizontally)
        {
            float x = Mathf.Clamp01(landmark.X);
            float y = Mathf.Clamp01(landmark.Y);
            if (flipHorizontally)
            {
                x = 1f - x;
            }

            // Preview uses GUI y-down coordinates, while gameplay landmarks are normalized y-up.
            y = 1f - y;

            return new Vector2(
                imageRect.x + x * imageRect.width,
                imageRect.y + y * imageRect.height);
        }

        private static Rect PreviewTexCoords(bool flipHorizontally)
        {
            return new Rect(
                flipHorizontally ? 1f : 0f,
                0f,
                flipHorizontally ? -1f : 1f,
                1f);
        }

        private static void DrawGuiLine(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f)
            {
                return;
            }

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawPauseButton()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            if (GUI.Button(
                new Rect(Screen.width - 122f, 18f, 104f, 30f),
                "Pause",
                buttonStyle))
            {
                SetPaused(true);
            }
        }

        private void DrawTemporarySlowMotionToggle()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            if (GUI.Button(
                new Rect(18f, Screen.height - 48f, 166f, 30f),
                temporarySlowMotionEnabled
                    ? "Slow Motion 0.2x: ON"
                    : "Slow Motion 0.2x: OFF",
                buttonStyle))
            {
                temporarySlowMotionEnabled = !temporarySlowMotionEnabled;
                if (!temporarySlowMotionEnabled)
                {
                    temporarySlowMotionArmed = false;
                    temporarySlowMotionActive = false;
                    if (!isPaused)
                    {
                        Time.timeScale = 1f;
                    }
                }
            }
        }

        private static void SetWindowResolution(int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
        }

        private void EnsureGuiStyles()
        {
            uiLabelStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = Color.white }
            };
        }

        private void DrawFrontend()
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0.025f, 0.035f, 0.045f, 0.96f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float leftWidth = Mathf.Clamp(Screen.width * 0.42f, 420f, 680f);
            GUI.color = new Color(0.04f, 0.06f, 0.075f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, leftWidth, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle(uiLabelStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 42,
                fontStyle = FontStyle.Bold
            };
            GUIStyle menuButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                padding = new RectOffset(16, 16, 8, 8)
            };

            GUI.Label(new Rect(54f, 70f, leftWidth - 90f, 70f), "VR BADMINTON", titleStyle);

            float menuButtonWidth = Mathf.Min(340f, leftWidth - 108f);
            float menuButtonX = (leftWidth - menuButtonWidth) * 0.5f;
            if (GUI.Button(new Rect(menuButtonX, 190f, menuButtonWidth, 46f), "Start Tutorial", menuButton))
            {
                screenState = ScreenState.Tutorial;
            }

            if (GUI.Button(new Rect(menuButtonX, 250f, menuButtonWidth, 46f), "Settings", menuButton))
            {
                settingsOpen = !settingsOpen;
            }

            if (GUI.Button(new Rect(menuButtonX, 310f, menuButtonWidth, 46f), "Quit Game", menuButton))
            {
                QuitGame();
            }

            if (screenState == ScreenState.MainMenu)
            {
                float buttonX = leftWidth + 60f;
                float buttonWidth = Mathf.Max(260f, Screen.width - buttonX - 70f);
                if (GUI.Button(
                    new Rect(buttonX, Screen.height * 0.32f, buttonWidth, Screen.height * 0.34f),
                    "ENTER MATCH",
                    new GUIStyle(GUI.skin.button) { fontSize = 30 }))
                {
                    screenState = ScreenState.ContinueOrNew;
                }
            }
            else if (screenState == ScreenState.ContinueOrNew)
            {
                DrawContinueOrNew(leftWidth);
            }
            else if (screenState == ScreenState.NewGameSetup)
            {
                DrawNewGameSetup(leftWidth);
            }
            else if (screenState == ScreenState.Tutorial)
            {
                DrawTutorialPlaceholder(leftWidth);
            }

            if (settingsOpen)
            {
                DrawSettings(false);
            }

            GUI.color = previousColor;
        }

        private void DrawContinueOrNew(float leftWidth)
        {
            float x = leftWidth + 70f;
            float width = Mathf.Max(300f, Screen.width - x - 80f);
            GUI.Label(
                new Rect(x, 120f, width, 50f),
                "MATCH",
                new GUIStyle(uiLabelStyle) { fontSize = 28 });

            GUI.enabled = hasSavedMatch;
            if (GUI.Button(new Rect(x, 210f, width, 58f), "Continue Match"))
            {
                ContinueMatch();
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, 286f, width, 58f), "New Match"))
            {
                screenState = ScreenState.NewGameSetup;
            }

            if (GUI.Button(new Rect(x, 362f, width, 42f), "Back"))
            {
                screenState = ScreenState.MainMenu;
            }
        }

        private void DrawNewGameSetup(float leftWidth)
        {
            float x = leftWidth + 70f;
            float width = Mathf.Max(340f, Screen.width - x - 80f);
            GUI.Label(new Rect(x, 70f, width, 44f), "NEW MATCH", new GUIStyle(uiLabelStyle) { fontSize = 28 });

            GUI.Label(new Rect(x, 130f, width, 28f), "Mode", uiLabelStyle);
            GUI.color = gameMode == GameMode.SinglePlayer
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x, 165f, width * 0.48f, 40f), "Single Player"))
            {
                gameMode = GameMode.SinglePlayer;
            }
            GUI.color = gameMode == GameMode.Multiplayer
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x + width * 0.52f, 165f, width * 0.48f, 40f), "Online (Coming Soon)"))
            {
                gameMode = GameMode.Multiplayer;
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x, 225f, width, 28f), "Difficulty", uiLabelStyle);
            const int difficultyCount = 6;
            for (int i = 0; i < difficultyCount; i++)
            {
                GUI.color = i == difficultyLevel ? new Color(1f, 0.82f, 0.22f, 1f) : Color.white;
                if (GUI.Button(
                    new Rect(
                        x + i * (width / difficultyCount),
                        260f,
                        width / difficultyCount - 7f,
                        38f),
                    $"N{i}"))
                {
                    ConfigureDifficulty(i);
                }
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x, 320f, width, 28f), "Score Format", uiLabelStyle);
            GUI.color = scoreTarget == 15
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x, 355f, width * 0.48f, 40f), "15 Points"))
            {
                scoreTarget = 15;
                scoreCap = 21;
            }
            GUI.color = scoreTarget == 21
                ? new Color(1f, 0.82f, 0.22f, 1f)
                : Color.white;
            if (GUI.Button(new Rect(x + width * 0.52f, 355f, width * 0.48f, 40f), "21 Points"))
            {
                scoreTarget = 21;
                scoreCap = 30;
            }

            GUI.color = Color.white;
            GUI.enabled = gameMode == GameMode.SinglePlayer;
            if (GUI.Button(new Rect(x, 430f, width, 54f), "START NEW MATCH"))
            {
                StartNewMatch();
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x, 500f, width, 40f), "Back"))
            {
                screenState = ScreenState.ContinueOrNew;
            }
        }

        private string GetModeLabel()
        {
            return gameMode == GameMode.SinglePlayer
                ? "Single Player"
                : "Online";
        }

        private void DrawTutorialPlaceholder(float leftWidth)
        {
            float x = leftWidth + 70f;
            float width = Mathf.Max(300f, Screen.width - x - 80f);
            GUI.Label(new Rect(x, 150f, width, 50f), "BEGINNER TUTORIAL", new GUIStyle(uiLabelStyle) { fontSize = 28 });
            GUI.Label(new Rect(x, 220f, width, 40f), "Coming soon", new GUIStyle(uiLabelStyle) { fontSize = 18 });
            if (GUI.Button(new Rect(x, 300f, width, 44f), "Back"))
            {
                screenState = ScreenState.MainMenu;
            }
        }

        private void DrawPauseMenu()
        {
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);

            float panelWidth = 320f;
            float panelHeight = 260f;
            float panelX = (Screen.width - panelWidth) * 0.5f;
            float panelY = (Screen.height - panelHeight) * 0.5f;

            GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.96f);
            GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(
                new Rect(panelX + 20f, panelY + 24f, panelWidth - 40f, 42f),
                "PAUSED",
                new GUIStyle(uiLabelStyle) { fontSize = 28 });

            if (GUI.Button(
                new Rect(panelX + 60f, panelY + 82f, panelWidth - 120f, 38f),
                "Continue",
                buttonStyle))
            {
                SetPaused(false);
            }

            if (GUI.Button(
                new Rect(panelX + 60f, panelY + 132f, panelWidth - 120f, 38f),
                "Settings",
                buttonStyle))
            {
                settingsOpen = !settingsOpen;
            }

            if (GUI.Button(
                new Rect(panelX + 60f, panelY + 182f, panelWidth - 120f, 38f),
                "Main Menu",
                buttonStyle))
            {
                ReturnToMainMenu();
            }
        }

        private void SetPaused(bool paused)
        {
            isPaused = paused;
            settingsOpen = false;
            Time.timeScale = isPaused
                ? 0f
                : temporarySlowMotionActive ? 0.2f : 1f;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

    }

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

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
