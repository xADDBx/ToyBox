﻿// Copyright < 2021 > Narria (github user Cabarius) - License: MIT
using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.AreaLogic.QuestSystem;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.EventConditionActionSystem.Conditions;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Interaction;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View.MapObjects;
using ModKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static Kingmaker.UnitLogic.Interaction.SpawnerInteractionPart;
using static ToyBox.BlueprintExtensions;

namespace ToyBox {

    public static partial class BlueprintExtensions {
        public class IntrestingnessEntry {
            public BaseUnitEntity unit { get; set; }
            public object source { get; set; }
            public ConditionsChecker checker { get; set; }
            public List<Element>? elements { get; set; }
            public bool HasConditions => checker?.Conditions.Length > 0;
            public bool HasElements => elements?.Count > 0;
            public IntrestingnessEntry(BaseUnitEntity unit, object source, ConditionsChecker checker, List<Element>? elements = null) {
                this.unit = unit;
                this.source = source;
                this.checker = checker;
                this.elements = elements;
            }
        }
        public static bool IsActive(this IntrestingnessEntry entry) =>
            (entry.checker?.IsActive() ?? false)
            || (entry?.elements.Any(element => {
                try {
                    return element.IsActive();
                } catch (Exception ex) {
                    Mod.Debug(ex.ToString());
                    return false;
                }
            }) ?? false)
            || (entry?.elements?.Count > 0 && entry.source is ActionsHolder) // Kludge until we get more clever about analyzing dialog state.  This lets Lathimas show up as active
            ;
        public static bool IsActive(this Element element) => element switch {
            Conditional conditional => conditional.ConditionsChecker.Check(),
            Condition condition => condition.CheckCondition(),
            _ => false,
        };
        public static bool IsActive(this ConditionsChecker checker) => checker.Conditions.Any(c => c.CheckCondition());
        public static string CaptionString(this Condition condition) =>
            $"{condition.GetCaption().Orange()} -> {(condition.CheckCondition() ? "True".Green() : "False".Yellow())}";
        public static string CaptionString(this Element element) => $"{element.GetCaption().Orange()}";

        public static bool IsQuestRelated(this Element element) => element is GiveObjective
                                                                   || element is SetObjectiveStatus
                                                                   || element is StartEtude
                                                                   || element is CompleteEtude
                                                                   || element is UnlockFlag
                                                                   // || element is StartDialog
                                                                   || element is ObjectiveStatus
                                                                   || element is ItemsEnough
                                                                   || element is Conditional
                                                                   ;
        public static int InterestingnessCoefficent(this MechanicEntity entity)
            => entity is BaseUnitEntity unit ? unit.InterestingnessCoefficent() : 0;
        public static int InterestingnessCoefficent(this BaseUnitEntity unit) => unit.GetUnitInteractionConditions().Count(entry => {
            try {
                return entry.IsActive();
            } catch (Exception ex) {
                Mod.Debug(ex.ToString());
                return false;
            }
        });
        public static List<BlueprintDialog> GetDialog(this BaseUnitEntity unit) {
            var dialogs = unit.Parts.m_Parts
                                         .OfType<UnitPartInteractions>()
                                         .SelectMany(p => p.m_Interactions)
                                         .OfType<Wrapper>()
                                         .Select(w => w.Source)
                                         .OfType<SpawnerInteractionDialog>()
                                         .Select(sid => sid.Dialog).ToList();
            return dialogs;
        }
        public static IEnumerable<IntrestingnessEntry> GetUnitInteractionConditions(this BaseUnitEntity unit) {
            var spawnInterations = unit.Parts.m_Parts
                               .OfType<UnitPartInteractions>()
                               .SelectMany(p => p.m_Interactions)
                               .OfType<Wrapper>()
                               .Select(w => w.Source);
            var result = new HashSet<IntrestingnessEntry>();
            var elements = new HashSet<IntrestingnessEntry>();

            // dialog
            var dialogInteractions = spawnInterations.OfType<SpawnerInteractionDialog>().ToList();
            // dialog interation conditions
            var dialogInteractionConditions = dialogInteractions
                                        .Where(di => di.Conditions?.Get() != null)
                                        .Select(di => new IntrestingnessEntry(unit, di.Dialog, di.Conditions.Get().Conditions));
            result.UnionWith(dialogInteractionConditions.ToHashSet());
            // dialog conditions
            var dialogConditions = dialogInteractions
                                   .Select(di => new IntrestingnessEntry(unit, di.Dialog, di.Dialog.Conditions));
            result.UnionWith(dialogConditions.ToHashSet());
            // dialog elements
            var dialogElements = dialogInteractions
                .Select(di => new IntrestingnessEntry(unit, di.Dialog, null, di.Dialog.ElementsArray.Where(e => e.IsQuestRelated()).ToList()));
            elements.UnionWith(dialogElements.ToHashSet());
            // dialog cue conditions
            var dialogCueConditions = dialogInteractions
                                      .Where(di => di.Dialog.FirstCue != null)
                                      .SelectMany(di => di.Dialog.FirstCue.Cues
                                                          .Where(cueRef => cueRef.Get() != null)
                                                          .Select(cueRef => new IntrestingnessEntry(unit, cueRef.Get(), cueRef.Get().Conditions)));
            result.UnionWith(dialogCueConditions.ToHashSet());

            // actions
            var actionInteractions = spawnInterations.OfType<SpawnerInteractionActions>();
            // action interaction conditions
            var actionInteractionConditions = actionInteractions
                                              .Where(ai => ai.Conditions?.Get() != null)
                                              .Select(ai => new IntrestingnessEntry(unit, ai, ai.Conditions.Get().Conditions));
            result.UnionWith(actionInteractionConditions.ToHashSet());
            // action conditions
            var actionConditions = actionInteractions
                                   .SelectMany(ai => ai.ActionHolders)
                                   .Where(ai => ai?.Get() != null)
                                   .SelectMany(ai => ai.Get().Actions.Actions
                                                   .Where(a => a is Conditional)
                                                   .Select(a => new IntrestingnessEntry(unit, ai.Get(), (a as Conditional).ConditionsChecker)));
            result.Union(actionConditions.ToHashSet());
            // action elements
            var actionElements = actionInteractions
                .SelectMany(ai => ai.ActionHolders)
                .Where(ai => ai?.Get() != null)
                .Select(ai => new IntrestingnessEntry(unit, ai.Get(), null, ai.Get().ElementsArray.Where(e => e.IsQuestRelated()).ToList()));
            elements.UnionWith(actionElements.ToHashSet());
            foreach (var entry in elements) {
                //Mod.Debug($"checking {entry}");
                var conditionals = entry.elements.OfType<Conditional>();
                if (conditionals.Any()) {
                    //Mod.Debug($"found {conditionals.Count()} Conditionals");
                    foreach (var conditional in conditionals) {
                        var newEntry = new IntrestingnessEntry(entry.unit, conditional, conditional.ConditionsChecker);
                        result.Add(newEntry);
                        //Mod.Debug($"    Added {conditional}");
                    }
                    var nonConditionals = entry.elements.Where(element => !(element is Conditional));
                    entry.elements = nonConditionals.ToList();
                }
            }
            result.UnionWith(elements);
            return result;
        }
        public static void RevealInterestingNPCs() {
            if (Shodan.AllUnits
                is { } unitsPool) {
                var inerestingUnits = unitsPool.Where(u => u.InterestingnessCoefficent() > 0);
                foreach (var unit in inerestingUnits) {
                    Mod.Debug($"Revealing {unit.CharacterName}");
                }
            }
        }
    }
}