﻿// Copyright < 2023 >  - Narria (github user Cabarius) - License: MIT
using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.QuestSystem;
using Kingmaker.Designers;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.EventConditionActionSystem.Conditions;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using ModKit;
using ModKit.DataViewer;
using ModKit.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ModKit.UI;

namespace ToyBox {


    public class DialogAndNPCs {
        public static Settings Settings => Main.Settings;
        public static bool ShowInactive => Settings.toggleIntrestingNPCsShowFalseConditions;
        public static Player player => Game.Instance.Player;
        private static readonly Browser<BaseUnitEntity, BaseUnitEntity> ConditionsBrowser = new(Mod.ModKitSettings.searchAsYouType);

        public static void ResetGUI() { }

        public static void OnGUI() {
            if (!Main.IsInGame) return;
            DialogEditor.OnGUI();
            Div();
            10.space();
            using (HorizontalScope()) {
                Toggle("Mark Interesting NPCs on Map".localize(), ref Settings.toggleShowInterestingNPCsOnLocalMap, 375.width());
                HelpLabel("This will change the color of NPC names on the highlight makers and change the color map markers to indicate that they have interesting or conditional interactions".localize());
            }
            using (HorizontalScope()) {
                DisclosureToggle(RichText.Cyan("Interesting NPCs in the local area".localize()), ref Settings.toogleShowInterestingNPCsOnQuestTab);
                200.space();
                HelpLabel(("Show a list of NPCs that may have quest objectives or other interesting features " + RichText.Yellow("(Warning: Spoilers)")).localize());
            }
            if (Settings.toogleShowInterestingNPCsOnQuestTab) {
                using (HorizontalScope()) {
                    50.space();
                    using (VerticalScope(GUI.skin.box)) {
                        if (Shodan.AllBaseUnits is { } unitsPool) {
                            var units = Settings.toggleInterestingNPCsShowHidden ? unitsPool.All : unitsPool.ToList();
                            ConditionsBrowser.OnGUI(
                                (IEnumerable<BaseUnitEntity>)units.Where(u => u.InterestingnessCoefficent() >= 1),
                                () => units,
                                i => i,
                                u => u.CharacterName,
                                u => (new[] { u.CharacterName }),
                                () => {
                                    Toggle("Show Inactive Conditions", ref Settings.toggleIntrestingNPCsShowFalseConditions);
                                    if (ConditionsBrowser.ShowAll) {
                                        25.space();
                                        if (Toggle("Show other versions of NPCs", ref Settings.toggleInterestingNPCsShowHidden))
                                            ConditionsBrowser.ReloadData();
                                    }
#if DEBUG
                                    25.space();
                                    ActionButton("Reveal All On Map", BlueprintExtensions.RevealInterestingNPCs);
#endif
                                },
                                (u, _) => {
                                    var name = u.CharacterName;
                                    var coefficient = u.InterestingnessCoefficent();
                                    if (coefficient > 0)
                                        name = RichText.Orange(name);
                                    else
                                        name = RichText.Grey(name);
                                    Label(name, 600.width());
                                    175.space();
                                    Label(RichText.Grey($"Interestingness Coefficient: ") + RichText.Cyan(coefficient.ToString()));
                                    50.space();
                                    ReflectionTreeView.DetailToggle("Unit", u.Parts.m_Parts, u.Parts.m_Parts, 100);
                                    25.space();
                                    var dialogs = u.GetDialog();
                                    if (dialogs.Any())
                                        ReflectionTreeView.DetailToggle("Dialog", u, dialogs.Count == 1 ? dialogs.First() : dialogs, 100);
                                },
                                (u, _) => {
                                    ReflectionTreeView.OnDetailGUI(u.Parts.m_Parts);
                                    ReflectionTreeView.OnDetailGUI(u);
                                    var entries = u.GetUnitInteractionConditions();
                                    var checkerEntries = entries.Where(e => e.HasConditions && (ShowInactive || e.IsActive()));
                                    var conditions =
                                        from entry in checkerEntries
                                        from condition in entry.checker.Conditions
                                        group (condition, entry) by condition.GetCaption()
                                        into g
                                        select g.Select(p => (p.condition, new object[] { p.entry.source } as IEnumerable<object>))
                                                .Aggregate(((Condition condition, IEnumerable<object>) p, (Condition condition, IEnumerable<object>) q)
                                                               => (p.condition, (IEnumerable<object>)p.Item2.Concat(q.Item2))
                                                    );
                                    var elementEntries = entries.Where(e => e.HasElements && (ShowInactive || e.IsActive()));
                                    if (conditions.Any()) {
                                        using (HorizontalScope()) {
                                            115.space();
                                            Label(RichText.Yellow("Conditions"));
                                        }
                                    }
                                    foreach (var entry in conditions) {
                                        OnGUI(entry.condition,
                                              string.Join(", ", (IEnumerable<string>)entry.Item2.Select(source => source.ToString())),
                                              150
                                            );
                                    }
                                    if (elementEntries.Any()) {
                                        using (HorizontalScope()) {
                                            115.space();
                                            Label(RichText.Yellow("Elements"));
                                        }
                                    }
                                    foreach (var entry in elementEntries) {
                                        foreach (var element in entry.elements.OrderBy(e => e.GetType().Name)) {
                                            OnGUI(element, entry.source);
                                        }
                                    }
                                },
                                50,
                                false,
                                true,
                                100,
                                300,
                                "",
                                true);
                        }
                    }
                }
            }

        }

        public static void OnGUI(Element element, object source, int indent = 150, bool forceShow = false) {
            if (!element.IsActive()
                && source is not ActionsHolder // kludge again for Actions holder for Lathimas
                && !Settings.toggleIntrestingNPCsShowFalseConditions
                && !forceShow
               ) return;
            using (HorizontalScope()) {
                Space(indent);
                switch (element) {
                    case ObjectiveStatus objectiveStatus:
                        OnGUI(objectiveStatus, source);
                        break;
                    case QuestStatus questStatus:
                        OnGUI(questStatus, source);
                        break;
                    case EtudeStatus etudeStatus:
                        OnGUI(etudeStatus, source);
                        break;
                    case Conditional conditional:
                        OnGUI(conditional, source);
                        break;
                    case Condition condition:
                        OnGUI(condition, source);
                        break;
                    default:
                        OnOtherElementGUI(element, source);
                        break;
                }
            }
        }
        public static void OnGUI(ConditionsChecker checker, object source, int indent = 150, bool forceShow = false) {
            foreach (var condition in checker.Conditions.OrderBy(c => c.GetType().Name)) {
                OnGUI(condition, source, indent, forceShow);
            }
        }
        public static void OnGUI(Conditional conditional, object source) {
            if (conditional.ConditionsChecker.Conditions.Any()) {
                Label(RichText.Cyan("Conditional:".localize()), 150.width());
                //Label(string.Join(", ", conditional.ConditionsChecker.Conditions.Select(c => c.GetCaption())));
                Label(conditional.Comment, 375.width());
                using (VerticalScope()) {
                    OnGUI(conditional.ConditionsChecker, source, 0, true);
                }
            }
        }
        public static void OnGUI(QuestStatus questStatus, object source) {
            Label(RichText.Cyan("Quest Status: ".localize()), 150.width());
            var quest = questStatus.Quest;
            var state = GameHelper.Quests.GetQuestState(quest);
            var title = $"{RichText.Bold(RichText.Orange(quest.Title.StringValue()))}";
            Label(title, 500.width());
            22.space();
            using (VerticalScope()) {
                HelpLabel(quest.Description);
                Label(RichText.Cyan($"status: ".localize()) + state.ToString());
                Label(RichText.Cyan("condition: ".localize()) + questStatus.CaptionString());
                Label(RichText.Cyan("source: ".localize()) + RichText.Yellow(source.ToString()));
            }
        }
        public static void OnGUI(ObjectiveStatus objectiveStatus, object source) {
            Label(RichText.Cyan("Objective Status: ".localize()), 150.width());

            var objectiveBP = objectiveStatus.QuestObjective;
            var objective = Game.Instance.Player.QuestBook.GetObjective(objectiveBP);
            var quest = objectiveBP.Quest;
            var state = objective?.State ?? QuestObjectiveState.None;
            var title = $"{RichText.Bold(RichText.Orange(quest.Title.StringValue()))} : {objective.titleColored(objectiveBP)}";
            Label(title, 500.width());
            22.space();
            using (VerticalScope()) {
                HelpLabel(objectiveBP.Description);
                Label(RichText.Cyan($"status: ".localize()) + state.ToString().titleColored(state));
                Label(RichText.Cyan("condition: ".localize()) + objectiveStatus.CaptionString());
                Label(RichText.Cyan("source: ".localize()) + RichText.Yellow(source.ToString()));
            }
        }
        public static void OnGUI(EtudeStatus etudeStatus, object source) {
            Label(RichText.Cyan("Etude Status: ".localize()), 150.width());
            var etudeBP = etudeStatus.Etude;
            Label(RichText.Orange(etudeBP.name), 500.width());
            var etudeState = Game.Instance.Player.EtudesSystem.GetSavedState(etudeBP);
            var debugInfo = Game.Instance.Player.EtudesSystem.GetDebugInfo(etudeBP);
            22.space();
            using (VerticalScope()) {
                HelpLabel(debugInfo);
                Label(RichText.Cyan($"status: ".localize()) + etudeState.ToString());
                Label(RichText.Cyan("condition: ".localize()) + etudeStatus.CaptionString());
                Label(RichText.Cyan("source: ".localize()) + RichText.Yellow(source.ToString()));
            }
        }
        public static void OnGUI(Condition condition, object source) {
            Label(RichText.Cyan($"{condition.GetType().Name}:"), 150.width());
            Label(RichText.Yellow(source.ToString()), 500.width());
            22.space();
            using (VerticalScope()) {
                Label(RichText.Cyan("condition: ".localize()) + condition.CaptionString());
            }
        }
        public static void OnOtherElementGUI(Element element, object source) {
            Label(RichText.Cyan($"{element.GetType().Name}:"), 150.width());
            Label(RichText.Yellow(source.ToString()), 500.width());
            22.space();
            using (VerticalScope()) {
                Label(RichText.Cyan("caption: ".localize()) + element.CaptionString());
            }
        }
    }
}
