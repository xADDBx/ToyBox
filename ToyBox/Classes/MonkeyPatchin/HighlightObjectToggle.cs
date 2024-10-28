﻿using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.GameModes;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.InputSystems;
using Kingmaker.UI.Models.SettingsUI;
using Kingmaker.Utility.GameConst;
using Kingmaker.View.MapObjects;
using Kingmaker.View.MapObjects.InteractionComponentBase;
using ModKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ToyBox.classes.MonkeyPatchin {
    public class HighlightObjectToggle : IGameModeHandler {
        private static HashSet<GameModeType> TurnOffWhen = [GameModeType.Dialog, GameModeType.Cutscene, GameModeType.CutsceneGlobalMap];
        private static bool wasTurnedOffBefore = false;
        internal static bool wasTurnedOff = false;
        public void OnGameModeStart(GameModeType gameMode) {
            if (!Main.Settings.highlightObjectsToggle) return;
            if (Game.Instance.Player.IsInCombat) return;
            if (TurnOffWhen.Contains(gameMode)) {
                if (InteractionHighlightController.Instance?.IsHighlighting ?? false) {
                    wasTurnedOffBefore = true;
                    wasTurnedOff = true;
                    InteractionHighlightController.Instance?.HighlightOff();
                    wasTurnedOff = false;
                }
            } else {
                if (wasTurnedOffBefore && (!InteractionHighlightController.Instance?.IsHighlighting ?? false)) {
                    InteractionHighlightController.Instance?.HighlightOn();
                    wasTurnedOffBefore = false;
                }
            }
        }
        public void OnGameModeStop(GameModeType gameMode) {
            return;
        }

        [HarmonyPatch(typeof(KeyboardAccess))]
        private static class KeyboardAccess_Patch {
            public static bool justChanged = false;
            [HarmonyPatch(nameof(KeyboardAccess.OnCallbackByBinding))]
            [HarmonyPrefix]
            private static bool OnCallbackByBinding(KeyboardAccess.Binding binding) {
                if (!Main.Settings.highlightObjectsToggle) return true;
                if (Game.Instance?.Player?.IsInCombat ?? false) return true;
                if (binding.Name.StartsWith(UISettingsRoot.Instance.UIKeybindGeneralSettings.HighlightObjects.name)) {
                    if (binding.Name.EndsWith(UIConsts.SuffixOn) && binding.InputMatched() && !justChanged) {
                        justChanged = true;
                        try {
                            InteractionHighlightController.Instance?.Highlight(!InteractionHighlightController.Instance?.IsHighlighting ?? false);
                        } catch {
                            justChanged = false;
                            return false;
                        }
                        Task.Run(() => {
                            Thread.Sleep(250);
                            justChanged = false;
                        });
                    }
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(Player))]
        private static class Player_Patch {
            private static bool interestingTick = false;
            private static bool wasOnBeforeFightIntern = false;
            internal static bool wasOnBeforeFight = false;
            [HarmonyPatch(nameof(Player.IsInCombat), MethodType.Setter)]
            [HarmonyPrefix]
            private static void setIsInCombatPre(bool value) {
                if (!Main.Settings.highlightObjectsToggle) return;
                interestingTick = value != Game.Instance.Player.IsInCombat;
                if (!interestingTick) return;
                if ((InteractionHighlightController.Instance?.IsHighlighting ?? false) && value) {
                    wasOnBeforeFightIntern = true;
                    wasOnBeforeFight = true;
                    try {
                        InteractionHighlightController.Instance.HighlightOff();
                    } catch { }
                    wasOnBeforeFight = false;
                }
            }
            [HarmonyPatch(nameof(Player.IsInCombat), MethodType.Setter)]
            [HarmonyPostfix]
            private static void SetIsInCombatPost(bool value) {
                if (!Main.Settings.highlightObjectsToggle) return;
                if (!interestingTick) return;
                if (wasOnBeforeFightIntern && !value) {
                    wasOnBeforeFightIntern = false;
                    try {
                        InteractionHighlightController.Instance?.HighlightOn();
                    } catch { }
                }
                interestingTick = false;
            }
        }
        [HarmonyPatch(typeof(InteractionHighlightController))]
        private static class InteractionHighlightController_Patch {
            [HarmonyPatch(nameof(InteractionHighlightController.HighlightOff))]
            [HarmonyPrefix]
            private static bool HighlightOff() {
                if (!Main.Settings.highlightObjectsToggle) return true;
                if (Game.Instance.Player.IsInCombat) return true;
                if (!Player_Patch.wasOnBeforeFight && !wasTurnedOff && !KeyboardAccess_Patch.justChanged) {
                    return false;
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(MapObjectView))]
        private static class MapObjectView_Patch {
            [HarmonyPatch(nameof(MapObjectView.ShouldBeHighlighted))]
            [HarmonyPostfix]
            private static void ShouldBeHighlighted(MapObjectView __instance, ref bool __result) {
                if (__instance == null) return;
                if (!Main.Settings.highlightHiddenObjects && !Main.Settings.highlightHiddenObjectsInFog) return;
                bool flag = __instance.Highlighted || __instance.m_ForcedHighlightOnReveal || (__instance.GlobalHighlighting && (!__instance.Data.IsInFogOfWar || Main.Settings.highlightHiddenObjectsInFog));
                if (Game.Instance.TurnController.TurnBasedModeActive) {
                    if (__instance.Data.Parts.GetAll<InteractionPart>().Any((InteractionPart i) => i is InteractionLootPart)) {
                        flag = false;
                    }
                }
                bool HighlightOnHover = (__instance.Data.IsRevealed || Main.Settings.highlightHiddenObjects) && (__instance.CanBeAttackedDirectly || __instance.Data.Parts.GetAll<InteractionPart>().Any(i => {
                    InteractionType type = i.Type;
                    return (type == InteractionType.Approach || type == InteractionType.Direct) && i.Enabled && (!i.Settings.ShowOvertip || (i.Settings.ShowOvertip && i.Settings.ShowHighlight));
                }));
                if (!flag || !HighlightOnHover || ((__instance.Data.IsRevealed || !__instance.Data.IsAwarenessCheckPassed) && !Main.Settings.highlightHiddenObjects)) {
                    __result = __instance.Data.Parts.GetAll<InteractionPart>().Any((InteractionPart i) => i.HasVisibleTrap());
                } else {
                    __result = true;
                }
                Mod.Debug($"Checking highlighting for {__instance.name}: Result:{__result}; flag:{flag}; Highlighted:{__instance.Highlighted} - ForcedHighlightOnReveal:{__instance.m_ForcedHighlightOnReveal} - GlobalHighlighting:{__instance.GlobalHighlighting} - IsInFogOfWar:{__instance.Data.IsInFogOfWar} - HighlightOnHover:{__instance.HighlightOnHover} - IsRevealed:{__instance.Data.IsRevealed} - AwarenessCheckPassed:{__instance.Data.IsAwarenessCheckPassed}");
            }
        }
    }
}