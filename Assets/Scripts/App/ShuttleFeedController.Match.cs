using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using VRBadminton.Gameplay;
using VRBadminton.Input;

namespace VRBadminton.App
{
    public sealed partial class ShuttleFeedController
    {
        private IEnumerator GameLoop()
        {
            yield return new WaitForSeconds(firstFeedDelay);
            while (enabled)
            {
                if (gameMode != GameMode.SinglePlayer)
                {
                    yield return null;
                    continue;
                }

                rallyWinner = 0;
                opponentStamina = opponentMaxStamina;

                if (playerServing)
                {
                    yield return PlayerServe();
                }
                else
                {
                    yield return FeedOneShuttle();
                }

                if (temporarySlowMotionActive)
                {
                    temporarySlowMotionActive = false;
                    temporarySlowMotionArmed = false;
                    Time.timeScale = 1f;
                }

                MatchState state = new MatchState
                {
                    PlayerScore = playerScore,
                    OpponentScore = opponentScore,
                    PlayerServing = playerServing
                };
                state = RallyOutcomeResolver.ApplyRallyWinner(
                    state,
                    rallyWinner,
                    CurrentMatchRules());
                playerScore = state.PlayerScore;
                opponentScore = state.OpponentScore;
                playerServing = state.PlayerServing;
                matchWinner = state.MatchWinner;

                if (matchWinner != 0)
                {
                    matchOver = true;
                    yield break;
                }

                yield return new WaitForSeconds(delayBetweenFeeds);
            }
        }

        private int GetMatchWinner()
        {
            return CurrentMatchRules().GetWinner(playerScore, opponentScore);
        }

        private MatchRules CurrentMatchRules()
        {
            return new MatchRules(scoreTarget, scoreCap);
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void ConfigureDifficulty(int level)
        {
            DifficultyTuning tuning = DifficultyTuning.ForLevel(level);
            difficultyLevel = tuning.Level;
            opponentMaxStamina = tuning.OpponentMaxStamina;
            opponentSmashChance = tuning.OpponentSmashChance;
            opponentSmashReceiveChance = tuning.OpponentSmashReceiveChance;

            opponentStamina = opponentMaxStamina;
        }

        private void StartNewMatch()
        {
            if (gameMode != GameMode.SinglePlayer)
            {
                return;
            }

            playerScore = 0;
            opponentScore = 0;
            playerServing = false;
            hasSavedMatch = true;
            BeginGameplay();
        }

        private void ContinueMatch()
        {
            if (!hasSavedMatch)
            {
                return;
            }

            BeginGameplay();
        }

        private void BeginGameplay()
        {
            StopAllCoroutines();
            screenState = ScreenState.Playing;
            isPaused = false;
            settingsOpen = false;
            Time.timeScale = 1f;
            rallyWinner = 0;
            matchWinner = 0;
            matchOver = false;
            ResetRallyVisuals();
            StartCoroutine(GameLoop());
        }

        private void ReturnToMainMenu()
        {
            hasSavedMatch = true;
            StopAllCoroutines();
            isPaused = false;
            settingsOpen = false;
            Time.timeScale = 0f;
            screenState = ScreenState.MainMenu;
            rallyWinner = 0;
            ResetRallyVisuals();
        }

        private void ResetRallyVisuals()
        {
            temporarySlowMotionArmed = false;
            temporarySlowMotionActive = false;
            if (!isPaused && screenState == ScreenState.Playing)
            {
                Time.timeScale = 1f;
            }
            shuttleIncoming = false;
            incomingHighClear = false;
            incomingOpponentSmash = false;
            smashReceiveReady = false;
            awaitingPlayerServe = false;
            awaitingOpponentServe = false;
            opponentServeReady = false;
            swingPending = false;
            pendingSwingStartedAt = 0f;
            hasPlayerContactPrediction = false;
            ClearHitHistory();
            shuttleTrail.emitting = false;
            shuttle.gameObject.SetActive(false);
            landingMarker.gameObject.SetActive(false);
            playerPositionMarker.gameObject.SetActive(false);
            trajectoryGuide.gameObject.SetActive(false);
            HideLandingPrediction();
            jumpActive = false;
            jumpOffset = 0f;
            opponentReturningToCenter = false;
        }

        private void RestartMatch()
        {
            StopAllCoroutines();
            playerScore = 0;
            opponentScore = 0;
            rallyWinner = 0;
            matchWinner = 0;
            matchOver = false;
            playerServing = false;
            opponentStamina = opponentMaxStamina;
            ResetRallyVisuals();
            StartCoroutine(GameLoop());
        }

    }
}
