﻿using Kingmaker;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.JsonSystem.EditorDatabase;
using ModKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace ToyBox {
    public class EtudesTreeModel {
        public IEnumerable<BlueprintEtude> etudes;
        public NamedTypeFilter<BlueprintEtude> etudeFilter = new("Etudes", null, bp => bp.CollationNames(bp.Parent?.GetBlueprint().NameSafe() ?? ""));
        public Dictionary<string, EtudeInfo> loadedEtudes = new();
        public Dictionary<string, ConflictingGroupIdReferences> conflictingGroups = new();

        public Dictionary<string, string> commentTranslations;
        private EtudesTreeModel() {
            commentTranslations = Utils.ReadTranslations();
            Mod.Debug($"loaded {commentTranslations.Count} key/value pairs");
        }

        private static EtudesTreeModel _instance;

        public static EtudesTreeModel Instance => _instance ??= new EtudesTreeModel();

        public void ReloadBlueprintsTree() {
            etudes = BlueprintLoader.Shared.GetBlueprintsOfType<BlueprintEtude>();
            if ((etudes?.Count() ?? 0) == 0) return;
            loadedEtudes = new Dictionary<string, EtudeInfo>();
            var filteredEtudes = (from bp in etudes
                                  where etudeFilter.filter(bp)
                                  select bp).ToList();
            foreach (var etude in filteredEtudes) {
                AddEtudeToLoaded(etude);
            }

            foreach (var loadedEtude in loadedEtudes) {
                foreach (var etude in loadedEtude.Value.ChainedId) {
                    loadedEtudes[etude].ChainedTo = loadedEtude.Key;
                }

                foreach (var etude in loadedEtude.Value.LinkedId) {
                    loadedEtudes[etude].LinkedTo = loadedEtude.Key;
                }
            }
        }

        public void UpdateEtude(BlueprintEtude blueprintEtude) {
            if (loadedEtudes.ContainsKey(blueprintEtude.AssetGuid)) {
                UpdateEtudeData(blueprintEtude);
            } else {
                AddEtudeToLoaded(blueprintEtude);
            }
        }

        private void UpdateEtudeData(BlueprintEtude blueprintEtude) {
            var etudeInfo = PrepareNewEtudeData(blueprintEtude);
            var oldEtude = loadedEtudes[blueprintEtude.AssetGuid];
            //Remove old data
            if (etudeInfo.ChainedTo != oldEtude.ChainedTo && oldEtude.ChainedTo != string.Empty && loadedEtudes[oldEtude.ChainedTo].ChainedId.Contains(blueprintEtude.AssetGuid))
                loadedEtudes[oldEtude.ChainedTo].ChainedId.Remove(blueprintEtude.AssetGuid);
            if (etudeInfo.LinkedTo != oldEtude.LinkedTo && oldEtude.LinkedTo != string.Empty && loadedEtudes[oldEtude.LinkedTo].LinkedId.Contains(blueprintEtude.AssetGuid))
                loadedEtudes[oldEtude.LinkedTo].LinkedId.Remove(blueprintEtude.AssetGuid);
            if (etudeInfo.ParentId != oldEtude.ParentId && oldEtude.ParentId != string.Empty && loadedEtudes[oldEtude.ParentId].ChildrenId.Contains(blueprintEtude.AssetGuid))
                loadedEtudes[oldEtude.ParentId].ChildrenId.Remove(blueprintEtude.AssetGuid);

            foreach (var etude in oldEtude.ChainedId) {
                if (!etudeInfo.ChainedId.Contains(etude)) {
                    loadedEtudes[etude].ChainedTo = string.Empty;
                }
            }

            foreach (var etude in oldEtude.LinkedId) {
                if (!etudeInfo.LinkedId.Contains(etude)) {
                    loadedEtudes[etude].LinkedTo = string.Empty;
                }
            }

            //Add new data

            etudeInfo.ChildrenId = oldEtude.ChildrenId;
            etudeInfo.ChainedTo = oldEtude.ChainedTo;
            etudeInfo.LinkedTo = oldEtude.LinkedTo;
            if (oldEtude.ChainedTo != string.Empty)
                loadedEtudes[etudeInfo.ChainedTo].ChainedId.Add(blueprintEtude.AssetGuid);

            if (oldEtude.LinkedTo != string.Empty)
                loadedEtudes[etudeInfo.LinkedTo].LinkedId.Add(blueprintEtude.AssetGuid);

            loadedEtudes[blueprintEtude.AssetGuid] = etudeInfo;

            foreach (var etude in loadedEtudes[blueprintEtude.AssetGuid].ChainedId) {
                loadedEtudes[etude].ChainedTo = blueprintEtude.AssetGuid;
            }

            foreach (var etude in loadedEtudes[blueprintEtude.AssetGuid].LinkedId) {
                loadedEtudes[etude].LinkedTo = blueprintEtude.AssetGuid;
            }
        }

        private void AddEtudeToLoaded(BlueprintEtude blueprintEtude) {
            if (!loadedEtudes.ContainsKey(blueprintEtude.AssetGuid)) {
                var etudeInfo = PrepareNewEtudeData(blueprintEtude);
                loadedEtudes.Add(blueprintEtude.AssetGuid, etudeInfo);
            }
        }

        public void RemoveEtudeData(string SelectedId) {
            if (!loadedEtudes.ContainsKey(SelectedId))
                return;

            var etudeToRemove = loadedEtudes[SelectedId];
            loadedEtudes[etudeToRemove.ParentId].ChildrenId.Remove(SelectedId);

            if (etudeToRemove.LinkedTo != string.Empty) {
                loadedEtudes[etudeToRemove.LinkedTo].LinkedId.Remove(SelectedId);
            }

            if (etudeToRemove.ChainedTo != string.Empty) {
                loadedEtudes[etudeToRemove.ChainedTo].ChainedId.Remove(SelectedId);
            }

            foreach (var linkedTo in etudeToRemove.LinkedId) {
                loadedEtudes[linkedTo].LinkedTo = string.Empty;
            }

            foreach (var chainedTo in etudeToRemove.ChainedId) {
                loadedEtudes[chainedTo].ChainedTo = string.Empty;
            }

            loadedEtudes.Remove(SelectedId);
        }

        private EtudeInfo PrepareNewEtudeData(BlueprintEtude blueprintEtude) {
            if (blueprintEtude.Parent == null)
                EtudesEditor.rootEtudeId = blueprintEtude.AssetGuid;
            var etudeInfo = new EtudeInfo {
                Name = blueprintEtude.name,
                Blueprint = blueprintEtude,
                ParentId = blueprintEtude.Parent?.Get()?.AssetGuid ?? string.Empty,
                AllowActionStart = blueprintEtude.CanPlay(),
                CompleteParent = blueprintEtude.CompletesParent,
                Comment = blueprintEtude.Comment,
                Priority = blueprintEtude.Priority
            };

            if (etudeInfo?.Comment?.Length > 0) {
                if (commentTranslations.TryGetValue(etudeInfo.Comment.Trim(), out var translatedComment)) {
                    etudeInfo.Comment = translatedComment;
                }
            }

            foreach (var conflictingGroup in blueprintEtude.ConflictingGroups) {
                var conflictingGroupBlueprint = conflictingGroup.GetBlueprint();

                if (conflictingGroupBlueprint == null)
                    continue;

                etudeInfo.ConflictingGroups.Add(conflictingGroupBlueprint.AssetGuid);

                if (!conflictingGroups.ContainsKey(conflictingGroupBlueprint.AssetGuid))
                    conflictingGroups.Add(conflictingGroupBlueprint.AssetGuid, new ConflictingGroupIdReferences());

                conflictingGroups[conflictingGroupBlueprint.AssetGuid].Name = conflictingGroupBlueprint.name;

                if (!conflictingGroups[conflictingGroupBlueprint.AssetGuid].Etudes.Contains(blueprintEtude.AssetGuid))
                    conflictingGroups[conflictingGroupBlueprint.AssetGuid].Etudes.Add(blueprintEtude.AssetGuid);
            }

            if (blueprintEtude.LinkedAreaPart != null) {
                etudeInfo.LinkedArea = blueprintEtude.LinkedAreaPart.AssetGuid;
            }

            if (etudeInfo.ParentId != string.Empty) {
                if (!loadedEtudes.ContainsKey(blueprintEtude.Parent.Get().AssetGuid))
                    AddEtudeToLoaded(blueprintEtude.Parent.Get());

                if (!loadedEtudes[etudeInfo.ParentId].ChildrenId.Contains(blueprintEtude.AssetGuid))
                    loadedEtudes[etudeInfo.ParentId].ChildrenId.Add(blueprintEtude.AssetGuid);
            }

            foreach (var chainedStart in blueprintEtude.StartsOnComplete) {
                if (chainedStart.Get() == null)
                    continue;

                etudeInfo.ChainedId.Add(chainedStart.Get().AssetGuid);
            }

            foreach (var linkedStart in blueprintEtude.StartsWith) {
                if (linkedStart.Get() == null)
                    continue;


                etudeInfo.LinkedId.Add(linkedStart.Get().AssetGuid);
            }

            return etudeInfo;
        }
        public List<string> GetConflictingEtudes(string etudeID) {
            var result = new List<string>();

            foreach (var conflictingGroup in loadedEtudes[etudeID].ConflictingGroups) {
                foreach (var etude in Instance.conflictingGroups[conflictingGroup].Etudes) {
                    if (result.Contains(etude))
                        continue;
                    result.Add(etude);
                }
            }

            result = result.OrderBy(e => -loadedEtudes[e].Priority - ((loadedEtudes[e].State == EtudeInfo.EtudeState.Active) ? 100500 : 0)).ToList();

            return result;
        }
    }
}
