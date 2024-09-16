﻿using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Designers.EventConditionActionSystem.Evaluators;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.View.MapObjects;
using Kingmaker.View.MapObjects.InteractionRestrictions;
using ModKit;
using ModKit.Utility;
using System;
using System.Linq;
using UnityEngine;
using UnityModManagerNet;
using static ModKit.UI;

namespace ToyBox {
    public static class EnhancedUI {
        public static Settings Settings => Main.Settings;
        public static void ResetGUI() { }
        public static void OnGUI() {
            HStack("Common Tweaks".localize(),
                   1,
                   () => {
                       ActionButton("Maximize Window".localize(), Actions.MaximizeModWindow, 200.width());
                       300.space();
                       HelpLabel("Maximize the ModManager window for best ToyBox user experience".localize());
                   },
                   () => {
                       Toggle("Enhanced Map View".localize(), ref Settings.toggleZoomableLocalMaps, 500.width());
                       HelpLabel("Makes mouse zoom work for the local map (cities, dungeons, etc). Game restart required if you turn it off".localize());
                   },
                   () => {
                       var modifier = KeyBindings.GetBinding("InventoryUseModifier");
                       var modifierText = modifier.Key == KeyCode.None ? "Modifer" : modifier.ToString();
                       Toggle("Allow ".localize() + RichText.Cyan($"{modifierText}") + (RichText.Cyan(" + Click") + " To Use Items In Inventory").localize(), ref Settings.toggleShiftClickToUseInventorySlot, 470.width());
                       if (Settings.toggleShiftClickToUseInventorySlot) {
                           ModifierPicker("InventoryUseModifier", "", 0);
                       }
                   },
                   () => {
                       var modifier = KeyBindings.GetBinding("ClickToTransferModifier");
                       var modifierText = modifier.Key == KeyCode.None ? "Modifer" : modifier.ToString();
                       Toggle("Allow ".localize() + RichText.Cyan($"{modifierText}") + (RichText.Cyan(" + Click") + " To Transfer Entire Stack").localize(), ref Settings.toggleShiftClickToFastTransfer, 470.width());
                       if (Settings.toggleShiftClickToFastTransfer) {
                           ModifierPicker("ClickToTransferModifier", "", 0);
                       }
                   },
                   () => Toggle("Object Highlight Toggle Mode".localize(), ref Settings.highlightObjectsToggle),
                   () => {
                       Toggle("Mark Interesting NPCs".localize(), ref Settings.toggleShowInterestingNPCsOnLocalMap, 500.width());
                       HelpLabel("This will change the color of NPC names on the highlike makers and change the color map markers to indicate that they have interesting or conditional interactions".localize());
                   },
                   () => ActionButton("Fix Incorrect Main Character".localize(),
                                      () => {
                                          var probablyPlayer = Game.Instance.Player?.Party?
                                                                   .Where(x => !x.IsCustomCompanion())
                                                                   .Where(x => !x.IsStoryCompanion()).ToList();
                                          if (probablyPlayer is { Count: 1 }) {
                                              var newMainCharacter = probablyPlayer.First();
                                              Mod.Warn($"Promoting {newMainCharacter.CharacterName} to main character!");
                                              if (Game.Instance != null) Game.Instance.Player.MainCharacter = new UnitReference(newMainCharacter);
                                          }
                                      },
                                      AutoWidth()),
                   () => { }
                );
            Div(0, 25);
            HStack("Loot Rarity Coloring".localize(),
                   1,
                   () => {
                       using (VerticalScope(300.width())) {
                           Toggle("Show Rarity Tags".localize(), ref Settings.toggleShowRarityTags);
                           Toggle("Color Item Names".localize(), ref Settings.toggleColorLootByRarity);
                       }
                       using (VerticalScope()) {
                           Label(RichText.Green($"This makes loot function like Diablo or Borderlands. {RichText.Orange("Note: turning this off requires you to save and reload for it to take effect.")}".localize()
));
                       }
                   },
                   () => {
                       if (Settings.UsingLootRarity) {
                           using (VerticalScope(400.width())) {
                               Label(RichText.Cyan("Minimum Rarity For Loot Rarity Tags/Colors".localize()), AutoWidth());
                               RarityGrid(ref Settings.minRarityToColor, 4, AutoWidth());
                           }
                       }
                   },
                   () => {
                       if (Settings.UsingLootRarity) {
                           using (VerticalScope(300)) {
                               using (HorizontalScope(300)) {
                                   using (VerticalScope()) {
                                       Label(RichText.Cyan("Maximum Rarity To Hide:".localize()), AutoWidth());
                                       RarityGrid(ref Settings.maxRarityToHide, 4, AutoWidth());
                                   }
                               }
                           }
                           50.space();
                           using (VerticalScope()) {
                               Label("");
                               HelpLabel($"This hides map pins of loot containers containing at most the selected rarity. {RichText.Orange("Note: Changing settings requires reopening the map.")}".localize());
                           }
                       }
                   },
                   () => { }
                );
            Div(0, 25);
            EnhancedCamera.OnGUI();
            Div(0, 25);
            // TODO: Update EnumHelper.ValidFilterCategories for RT
        }
    }

}