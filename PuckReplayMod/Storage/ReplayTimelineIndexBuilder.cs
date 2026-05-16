using System;
using System.Collections.Generic;

namespace PuckReplayMod
{
    internal static class ReplayTimelineIndexBuilder
    {
        public static List<ReplayTimelineEntrySummary> Build(ReplaySessionData session, out int goalCount, out int markerCount)
        {
            List<ReplayGameSegmentSummary> gameSegments;
            return Build(session, out goalCount, out markerCount, out gameSegments);
        }

        public static List<ReplayTimelineEntrySummary> Build(ReplaySessionData session, out int goalCount, out int markerCount, out List<ReplayGameSegmentSummary> gameSegments)
        {
            goalCount = 0;
            markerCount = 0;
            List<ReplayTimelineEntrySummary> entries = new List<ReplayTimelineEntrySummary>();
            gameSegments = new List<ReplayGameSegmentSummary>();
            HashSet<int> periodStarts = new HashSet<int>();
            HashSet<int> periodEnds = new HashSet<int>();
            ReplayGameSegmentSummary activeGame = null;
            GameStatePayload latestGameState = null;
            if (session == null || session.Events == null)
            {
                return entries;
            }

            for (int i = 0; i < session.Events.Count; i++)
            {
                ReplayEventDto replayEvent = session.Events[i];
                if (replayEvent == null)
                {
                    continue;
                }

                if (replayEvent.Type == "GoalScored")
                {
                    GoalScoredPayload goal = replayEvent.Payload as GoalScoredPayload;
                    goalCount++;
                    entries.Add(new ReplayTimelineEntrySummary
                    {
                        Tick = Math.Max(0, replayEvent.Tick),
                        Type = "Goal",
                        Team = goal != null ? goal.Team : string.Empty,
                        Label = FormatGoalLabel(goal),
                        Tooltip = FormatGoalTooltip(goal)
                    });
                }
                else if (replayEvent.Type == "Marker")
                {
                    markerCount++;
                    entries.Add(new ReplayTimelineEntrySummary
                    {
                        Tick = Math.Max(0, replayEvent.Tick),
                        Type = "Marker",
                        Team = string.Empty,
                        Label = "Marker " + markerCount,
                        Tooltip = "Marker " + markerCount
                    });
                }
                else if (replayEvent.Type == "GameState")
                {
                    GameStatePayload gameState = replayEvent.Payload as GameStatePayload;
                    int tick = Math.Max(0, replayEvent.Tick);
                    if (ShouldResetPeriodMarkerState(latestGameState, gameState))
                    {
                        periodStarts.Clear();
                        periodEnds.Clear();
                    }

                    AddPeriodTimelineEntries(entries, periodStarts, periodEnds, tick, gameState);
                    ApplyGameSegmentState(gameSegments, ref activeGame, tick, gameState);
                    if (gameState != null)
                    {
                        latestGameState = gameState;
                    }
                }
                else if (replayEvent.Type == "InitialSnapshot")
                {
                    InitialSnapshotPayload snapshot = replayEvent.Payload as InitialSnapshotPayload;
                    if (snapshot != null)
                    {
                        int tick = Math.Max(0, replayEvent.Tick);
                        AddPeriodTimelineEntries(entries, periodStarts, periodEnds, tick, snapshot.GameState);
                        ApplyGameSegmentState(gameSegments, ref activeGame, tick, snapshot.GameState);
                        if (snapshot.GameState != null)
                        {
                            latestGameState = snapshot.GameState;
                        }
                    }
                }
            }

            if (activeGame != null)
            {
                activeGame.EndTick = Math.Max(activeGame.StartTick, session.Header != null ? session.Header.TotalTicks : activeGame.StartTick);
                ApplySegmentScore(activeGame, latestGameState);
            }

            entries.Sort(delegate(ReplayTimelineEntrySummary left, ReplayTimelineEntrySummary right)
            {
                int tickCompare = left.Tick.CompareTo(right.Tick);
                if (tickCompare != 0)
                {
                    return tickCompare;
                }

                return string.Compare(left.Type, right.Type, StringComparison.Ordinal);
            });
            return entries;
        }

        private static string FormatGoalLabel(GoalScoredPayload goal)
        {
            if (goal == null)
            {
                return "Goal";
            }

            string team = string.IsNullOrEmpty(goal.Team) ? "Goal" : goal.Team + " goal";
            string scorer = goal.Scorer != null && !string.IsNullOrEmpty(goal.Scorer.Username)
                ? " - " + goal.Scorer.Username
                : string.Empty;
            return team + scorer + " (" + Math.Max(0, goal.BlueScore) + "-" + Math.Max(0, goal.RedScore) + ")";
        }

        private static string FormatGoalTooltip(GoalScoredPayload goal)
        {
            if (goal == null)
            {
                return "Goal";
            }

            List<string> parts = new List<string>
            {
                FormatGoalLabel(goal)
            };

            string assistText = FormatAssistText(goal);
            if (!string.IsNullOrEmpty(assistText))
            {
                parts.Add(assistText);
            }

            if (goal.PuckSpeed > 0f || goal.PuckShotSpeed > 0f)
            {
                parts.Add("Puck " + goal.PuckSpeed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                    ", shot " + goal.PuckShotSpeed.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }

            return string.Join(" | ", parts.ToArray());
        }

        private static string FormatAssistText(GoalScoredPayload goal)
        {
            List<string> assists = new List<string>();
            if (goal.Assist != null && !string.IsNullOrEmpty(goal.Assist.Username))
            {
                assists.Add(goal.Assist.Username);
            }

            if (goal.SecondAssist != null && !string.IsNullOrEmpty(goal.SecondAssist.Username))
            {
                assists.Add(goal.SecondAssist.Username);
            }

            if (assists.Count == 0)
            {
                return string.Empty;
            }

            return "Assists: " + string.Join(", ", assists.ToArray());
        }

        private static void AddPeriodTimelineEntries(
            List<ReplayTimelineEntrySummary> entries,
            HashSet<int> periodStarts,
            HashSet<int> periodEnds,
            int tick,
            GameStatePayload gameState)
        {
            if (entries == null || gameState == null || gameState.Period <= 0)
            {
                return;
            }

            if (IsPeriodStartPhase(gameState.Phase) && periodStarts.Add(gameState.Period))
            {
                entries.Add(new ReplayTimelineEntrySummary
                {
                    Tick = tick,
                    Type = "PeriodStart",
                    Team = string.Empty,
                    Label = "P" + gameState.Period + " start",
                    Tooltip = "Period " + gameState.Period + " start"
                });
            }

            if (IsPeriodEndPhase(gameState.Phase) && periodEnds.Add(gameState.Period))
            {
                entries.Add(new ReplayTimelineEntrySummary
                {
                    Tick = tick,
                    Type = "PeriodEnd",
                    Team = string.Empty,
                    Label = "P" + gameState.Period + " end",
                    Tooltip = "Period " + gameState.Period + " end"
                });
            }
        }

        private static bool IsPeriodStartPhase(string phase)
        {
            return phase == "FaceOff" || phase == "Play";
        }

        private static bool IsPeriodEndPhase(string phase)
        {
            return phase == "Intermission" || phase == "GameOver" || phase == "PostGame";
        }

        private static bool ShouldResetPeriodMarkerState(GameStatePayload previous, GameStatePayload current)
        {
            if (current == null)
            {
                return false;
            }

            if (current.Phase == "Warmup" && (previous == null || previous.Phase != "Warmup"))
            {
                return true;
            }

            return current.Period == 1 &&
                current.Phase == "PreGame" &&
                previous != null &&
                (previous.Phase == "GameOver" || previous.Phase == "PostGame" || previous.Phase == "Warmup");
        }

        private static void ApplyGameSegmentState(List<ReplayGameSegmentSummary> gameSegments, ref ReplayGameSegmentSummary activeGame, int tick, GameStatePayload gameState)
        {
            if (gameSegments == null || gameState == null)
            {
                return;
            }

            if (activeGame == null && IsGameStartState(gameState))
            {
                activeGame = new ReplayGameSegmentSummary
                {
                    Index = gameSegments.Count + 1,
                    StartTick = tick,
                    EndTick = tick,
                    Label = "Game " + (gameSegments.Count + 1),
                    BlueScore = Math.Max(0, gameState.BlueScore),
                    RedScore = Math.Max(0, gameState.RedScore)
                };
                gameSegments.Add(activeGame);
                return;
            }

            if (activeGame == null)
            {
                return;
            }

            ApplySegmentScore(activeGame, gameState);
            if (IsGameEndState(gameState))
            {
                activeGame.EndTick = Math.Max(activeGame.StartTick, tick);
                activeGame.Label = FormatGameSegmentLabel(activeGame);
                activeGame = null;
            }
        }

        private static bool IsGameStartState(GameStatePayload gameState)
        {
            return gameState != null &&
                gameState.Period == 1 &&
                (gameState.Phase == "FaceOff" || gameState.Phase == "Play");
        }

        private static bool IsGameEndState(GameStatePayload gameState)
        {
            return gameState != null && (gameState.Phase == "GameOver" || gameState.Phase == "Warmup");
        }

        private static void ApplySegmentScore(ReplayGameSegmentSummary segment, GameStatePayload gameState)
        {
            if (segment == null || gameState == null)
            {
                return;
            }

            segment.BlueScore = Math.Max(0, gameState.BlueScore);
            segment.RedScore = Math.Max(0, gameState.RedScore);
            segment.Label = FormatGameSegmentLabel(segment);
        }

        private static string FormatGameSegmentLabel(ReplayGameSegmentSummary segment)
        {
            if (segment == null)
            {
                return "Game";
            }

            return "Game " + segment.Index + " (" + Math.Max(0, segment.BlueScore) + "-" + Math.Max(0, segment.RedScore) + ")";
        }
    }
}
