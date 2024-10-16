﻿using ModKit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ToyBox {
    public partial class SettingsUI {
        public static string cultureSearchText = "";
        public static CultureInfo? uiCulture;
        public static List<CultureInfo> cultures = new();
        public static void OnGUI() {
            HStack("Settings".localize(), 1,
                () => Label("Mono Version".localize() + $": {Type.GetType("Mono.Runtime")?.GetMethod("GetDisplayName", BindingFlags.NonPublic | BindingFlags.Static)?.Invoke(null, null)?.ToString()}"),
                () => {
                    ActionButton("Reset UI".localize(), Main.SetNeedsResetGameUI);
                    25.space();
                    Label(("Tells the game to reset the in game UI.".Green() + " Warning".Yellow() + " Using this in dialog or the book will dismiss that dialog which may break progress so use with care".Orange()).localize());
                },
                () => Toggle("Always try to update to latest mod version".localize(), ref Main.Settings.toggleAlwaysUpdate, AutoWidth()),
                () => {
                    Toggle("Enable Game Development Mode".localize(), ref Main.Settings.toggleDevopmentMode);
                    Space(25);
                    HelpLabel($"This turns on the developer console which lets you access cheat commands, shows a FPS window (hide with F11), etc.\n{"Warning: ".Yellow().Bold()}{"You may need to restart the game for this to fully take effect".Orange()}".localize());
                },
                () => Label(""),
                () => EnumGrid("Log Level".localize(), ref Main.Settings.loggingLevel, AutoWidth()),
                () => Label(""),
                () => Toggle("Strip HTML (colors) from Native Console".localize(), ref Main.Settings.stripHtmlTagsFromNativeConsole, AutoWidth()),
                () => Toggle("Enable Search as you type for Browsers (needs restart)".localize(), ref Mod.ModKitSettings.searchAsYouType, AutoWidth()),
#if DEBUG
                () => Toggle("Strip HTML (colors) from Logs Tab in Unity Mod Manager".localize(), ref Main.Settings.stripHtmlTagsFromUMMLogsTab),
#endif
                () => Toggle("Display guids in most tooltips, use shift + left click on items/abilities to copy guid to clipboard".localize(), ref Main.Settings.toggleGuidsClipboard, AutoWidth()),
                () => Toggle("Display risky options".localize(), ref Main.Settings.toggleRiskyToggles, AutoWidth()),
                () => Toggle("Check for Glyph Support".localize(), ref Mod.ModKitSettings.CheckForGlyphSupport, AutoWidth()),
                () => {
                    if (!Mod.ModKitSettings.CheckForGlyphSupport) Toggle("Use default Glyphs".localize(), ref Mod.ModKitSettings.UseDefaultGlyphs, AutoWidth());
                },
                () => Toggle("Use BPId Cache to speed up loading of specific types of Blueprints".localize(), ref Main.Settings.toggleUseBPIdCache, AutoWidth()),
                () => Toggle("Automatically rebuild the BPId Cache if necessary".localize(), ref Main.Settings.toggleAutomaticallyBuildBPIdCache, AutoWidth()),
                () => Toggle("Preload Blueprints".localize(), ref Main.Settings.togglePreloadBlueprints, AutoWidth()),
                () => Slider("Blueprint Loader Chunk Size".localize(), ref Main.Settings.BlueprintsLoaderChunkSize, 1, 50000, 100, "", AutoWidth()),
                () => Slider("Blueprint Loader Threads".localize(), ref Main.Settings.BlueprintsLoaderNumThreads, 1, 128, 3, "", AutoWidth()),
                () => Slider("Blueprint Loader Amount of Shards".localize(), ref Main.Settings.BlueprintsLoaderNumShards, 1, 1024, 32, "", AutoWidth()),
              () => { }
            );
#if true
            Div(0, 25);
            HStack("Localization".localize(), 1,
                () => {
                    if (Event.current.type != EventType.Repaint) {
                        uiCulture = CultureInfo.GetCultureInfo(Mod.ModKitSettings.uiCultureCode);
                        cultures = CultureInfo.GetCultures(CultureTypes.AllCultures).OrderBy(ci => ci.DisplayName).ToList();
                        if (Main.Settings.onlyShowLanguagesWithFiles) {
                            var languages = LocalizationManager.getLanguagesWithFile().ToHashSet();
                            cultures = cultures
                                       .Where(ci => languages.Contains(ci.Name))
                                       .OrderBy(ci => ci.DisplayName).
                                       ToList();
                        }
                    }
                    using (VerticalScope()) {
                        using (HorizontalScope()) {
                            Label("Current Culture".localize().Cyan(), Width(275));
                            Space(25);
                            Label($"{uiCulture.DisplayName}({uiCulture.Name})".Orange());
                            Space(25);
                            ActionButton("Export current locale to file".localize().Cyan(), () => LocalizationManager.Export());
                            Space(25);
                            LinkButton("Open the Localization Guide".localize(), "https://github.com/cabarius/ToyBox/wiki/Localization-Guide");
                        }
                        15.space();
                        using (HorizontalScope()) {
                            Toggle("Only show languages with existing localization files".localize(), ref Main.Settings.onlyShowLanguagesWithFiles);
                        }
                        Div(0, 25);
                        if (GridPicker<CultureInfo>("Culture", ref uiCulture, cultures, null, ci => $"{ci.Name.Cyan().Bold()} {ci.DisplayName.Orange()}", ref cultureSearchText, 6, rarityButtonStyle, Width(ummWidth - 350))) {
                            Mod.ModKitSettings.uiCultureCode = uiCulture.Name;
                            LocalizationManager.Update();
                        }
                    }
                },
                () => { }
            );
#endif
        }
    }
}
