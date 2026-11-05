using System.Collections.Generic;
using System.IO;
using RunesOfElements.Core;
using RunesOfElements.Data;
using UnityEditor;
using UnityEngine;

namespace RunesOfElements.Editor
{
    public static class RunesDataGenerator
    {
        private const string RootDir = "Assets/Data/RunesOfElements";

        [MenuItem("Tools/Runes of Elements/Generate All Game Data")]
        public static void GenerateAll()
        {
            EnsureDirs();

            var content = DefaultContent.Create();

            var runes = GenerateRunes();
            var statuses = GenerateStatuses();
            var mages = GenerateMages(content.Mages);
            var monsters = GenerateMonsters(content.Monsters);
            var blessings = GenerateBlessings(content.Blessings);
            var campaign = GenerateCampaign(content.Tower, content.Campaign, content.RunePool);

            // GameDatabase
            string dbPath = $"{RootDir}/GameDatabase.asset";
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(dbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, dbPath);
            }
            db.Configure(mages, monsters, blessings, runes, statuses, campaign);
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RunesDataGenerator] Успішно згенеровано GameDatabase з {mages.Count} магами, {monsters.Count} монстрами, {blessings.Count} картами, {runes.Count} рунами!");
        }

        private static void EnsureDirs()
        {
            string[] dirs = {
                RootDir,
                $"{RootDir}/Runes",
                $"{RootDir}/Mages",
                $"{RootDir}/Monsters",
                $"{RootDir}/Blessings",
                $"{RootDir}/Statuses",
                $"{RootDir}/Campaign",
                $"{RootDir}/Icons"
            };
            foreach (var d in dirs)
            {
                if (!AssetDatabase.IsValidFolder(d))
                {
                    string parent = Path.GetDirectoryName(d).Replace('\\', '/');
                    string leaf = Path.GetFileName(d);
                    AssetDatabase.CreateFolder(parent, leaf);
                }
            }
        }

        private static List<RuneDefinition> GenerateRunes()
        {
            var list = new List<RuneDefinition>();

            var specs = new[]
            {
                (RuneType.Fire, "Вогонь", new Color(1f, 0.35f, 0.05f), "Assets/Arena/UI_Sprites/Icon_Fire.png", "Завдає d6+2 шкоди. Коштує 2 концентрації."),
                (RuneType.Water, "Вода", new Color(0.2f, 0.55f, 1f), "Assets/Arena/UI_Sprites/Icon_Water.png", "Завдає d6 шкоди. Коштує 1 концентрацію."),
                (RuneType.Ice, "Лід", new Color(0.35f, 0.9f, 1f), "Assets/Arena/UI_Sprites/Icon_Crystal.png", "Завдає d6 шкоди. Коштує 1 концентрацію."),
                (RuneType.Lightning, "Блискавка", new Color(0.85f, 0.45f, 1f), "Assets/Arena/UI_Sprites/Icon_Void.png", "Завдає d6 шкоди. Коштує 1 концентрацію."),
                (RuneType.Beam, "Промінь", new Color(1f, 0.9f, 0.2f), "Assets/Arena/UI_Sprites/Icon_D8.png", "Форма: вражає обрану ціль і монстра в іншій лінії в тій самій колонці."),
                (RuneType.Sphere, "Сфера", new Color(0.85f, 0.2f, 0.2f), "Assets/Arena/UI_Sprites/Icon_D6.png", "Форма: вражає обрану ціль і сусідів у тому самому рядку (до 3 цілей).")
            };

            foreach (var sp in specs)
            {
                string p = $"{RootDir}/Runes/Rune_{sp.Item1}.asset";
                var r = AssetDatabase.LoadAssetAtPath<RuneDefinition>(p);
                if (r == null) { r = ScriptableObject.CreateInstance<RuneDefinition>(); AssetDatabase.CreateAsset(r, p); }
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(sp.Item4);
                r.Configure(sp.Item1, sp.Item2, sp.Item3, icon, sp.Item5);
                EditorUtility.SetDirty(r);
                list.Add(r);
            }
            return list;
        }

        private static List<StatusDefinition> GenerateStatuses()
        {
            var list = new List<StatusDefinition>();
            var specs = new[]
            {
                (StatusType.Frozen, "Заморожений", new Color(0.4f, 0.85f, 1f), "Пропускає атаку в цьому раунді."),
                (StatusType.Stunned, "Приголомшений", new Color(1f, 0.85f, 0.2f), "Пропускає наступну дію/атаку."),
                (StatusType.Burn, "Опік", new Color(1f, 0.35f, 0.05f), "Отримує N шкоди на початку раунду."),
                (StatusType.Poison, "Отрута", new Color(0.4f, 0.85f, 0.3f), "Отримує N шкоди наприкінці раунду."),
                (StatusType.Weakened, "Ослаблений", new Color(0.6f, 0.6f, 0.6f), "Зменшує власну атаку/шкоду на 1."),
                (StatusType.Shattered, "Розколотий", new Color(1f, 0.5f, 0.5f), "Збільшує отриману шкоду на 1."),
                (StatusType.Shield, "Щит", new Color(0.3f, 0.8f, 1f), "Поглинає вказану кількість шкоди або наступну атаку."),
                (StatusType.Inspired, "Натхнений", new Color(1f, 0.95f, 0.4f), "+1 концентрація на початку ходу."),
                (StatusType.Silence, "Мовчання", new Color(0.7f, 0.4f, 0.8f), "Забороняє застосовувати вказану стихію."),
                (StatusType.StoneSkin, "Кам'яна шкіра", new Color(0.7f, 0.65f, 0.6f), "Зменшує кожен отриманий удар на N.")
            };

            foreach (var sp in specs)
            {
                string p = $"{RootDir}/Statuses/Status_{sp.Item1}.asset";
                var s = AssetDatabase.LoadAssetAtPath<StatusDefinition>(p);
                if (s == null) { s = ScriptableObject.CreateInstance<StatusDefinition>(); AssetDatabase.CreateAsset(s, p); }
                s.Configure(sp.Item1, sp.Item2, sp.Item3, null, sp.Item4);
                EditorUtility.SetDirty(s);
                list.Add(s);
            }
            return list;
        }

        private static List<MageDefinition> GenerateMages(List<MageDef> defs)
        {
            var list = new List<MageDefinition>();
            var artMap = new Dictionary<string, (string path, Color col)>
            {
                { "pyromancer", ("Assets/Arena/HeroCards/Hero_Pyromancer.png", new Color(1f, 0.35f, 0.1f)) },
                { "hydromancer", ("Assets/Arena/HeroCards/Hero_Hydromancer.png", new Color(0.2f, 0.6f, 1f)) },
                { "electromancer", ("Assets/Arena/HeroCards/Hero_Electromancer.png", new Color(1f, 0.85f, 0.15f)) },
                { "cryomancer", ("Assets/Arena/HeroCards/Hero_Cryomancer.png", new Color(0.3f, 0.9f, 1f)) }
            };

            foreach (var d in defs)
            {
                string p = $"{RootDir}/Mages/Mage_{d.Id}.asset";
                var m = AssetDatabase.LoadAssetAtPath<MageDefinition>(p);
                if (m == null) { m = ScriptableObject.CreateInstance<MageDefinition>(); AssetDatabase.CreateAsset(m, p); }
                artMap.TryGetValue(d.Id, out var art);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(art.path);
                m.Configure(d, art.col, sprite);
                EditorUtility.SetDirty(m);
                list.Add(m);
            }
            return list;
        }

        private static List<MonsterDefinition> GenerateMonsters(List<MonsterDef> defs)
        {
            var list = new List<MonsterDefinition>();
            var artMap = new Dictionary<string, string>
            {
                // Level 1
                { "goblin", "Assets/asetcard/image 6.png" },
                { "skeleton", "Assets/asetcard/image 10.png" },
                { "spider", "Assets/asetcard/image 8.png" },
                { "flyer", "Assets/asetcard/image 9.png" },
                { "wolf", "Assets/asetcard/image 17.png" },
                { "rotten_guard", "Assets/asetcard/image 18.png" },
                { "fire_elemental", "Assets/asetcard/image 19.png" },
                { "necromancer", "Assets/asetcard/image 20.png" },

                // Level 2
                { "thunder_shaman", "Assets/asetcard/image 11.png" },
                { "shadow_assassin", "Assets/asetcard/image 12.png" },
                { "ice_golem", "Assets/asetcard/image 15.png" },
                { "rift_demon", "Assets/asetcard/image 16.png" },

                // Level 3
                { "ancient_sentinel", "Assets/asetcard/image 21.png" },
                { "thunder_lord", "Assets/asetcard/image 22.png" },
                { "rift_lord", "Assets/asetcard/image 23.png" },
                { "void_dragon", "Assets/asetcard/image 24.png" },

                // Bosses
                { "boss_chaos_archmage", "Assets/asetcard/image 29.png" },
                { "boss_ice_queen", "Assets/asetcard/image 25.png" },
                { "boss_ancient_guardian", "Assets/asetcard/image 26.png" }
            };

            foreach (var d in defs)
            {
                string p = $"{RootDir}/Monsters/Monster_{d.Id}.asset";
                var m = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(p);
                if (m == null) { m = ScriptableObject.CreateInstance<MonsterDefinition>(); AssetDatabase.CreateAsset(m, p); }
                artMap.TryGetValue(d.Id, out string path);
                var sprite = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
                m.Configure(d, sprite);
                EditorUtility.SetDirty(m);
                list.Add(m);
            }
            return list;
        }

        private static List<BlessingCardDefinition> GenerateBlessings(List<BlessingDef> defs)
        {
            var list = new List<BlessingCardDefinition>();
            var artMap = new Dictionary<string, string>
            {
                { "blessing_power_flow", "Assets/asetcard/image 9.png" },
                { "blessing_double_swap", "Assets/asetcard/image 8.png" },
                { "blessing_tempered_will", "Assets/asetcard/image 26.png" },
                { "blessing_thirst_knowledge", "Assets/asetcard/image 18.png" },
                { "blessing_resonance", "Assets/asetcard/magichnyi_rezonans_card_no_category.png" },
                { "blessing_titan_blood", "Assets/asetcard/krov_tytana_no_category.png" },
                { "blessing_sorter_luck", "Assets/asetcard/shchastya_sortuvalnyka_no_category.png" },
                { "blessing_mage_hand", "Assets/asetcard/ruka_maha_no_category.png" },
                { "blessing_elemental_bond", "Assets/asetcard/stykhiynyi_zvyazok_no_category.png" },
                { "blessing_hope_radiance", "Assets/asetcard/syaivo_nadii_nocat.png" },
                { "curse_magic_fatigue", "Assets/asetcard/magichna_vtoma_no_category.png" },
                { "curse_resonance_crack", "Assets/asetcard/trishchyna_v_rezonansi_no_category.png" },
                { "curse_vain_thirst", "Assets/asetcard/marna_spraga_no_category.png" },
                { "curse_heavy_hand", "Assets/asetcard/vazhka_ruka_nocat.png" },
                { "curse_rift_whisper", "Assets/asetcard/shepit_rozlomu_nocat.png" },
                { "risk_chaos_roulette", "Assets/asetcard/ruletka_haosu_no_category.png" },
                { "risk_double_price", "Assets/asetcard/podviina_tsina_nocat.png" },
                { "risk_silence_pact", "Assets/asetcard/pakt_movchannia_nocat.png" },
                { "risk_fate_exchange", "Assets/asetcard/obmin_doleyu_nocat.png" },
                { "risk_fortune_mirror", "Assets/asetcard/dzerkalo_fortuny_nocat.png" }
            };

            foreach (var d in defs)
            {
                string p = $"{RootDir}/Blessings/Blessing_{d.Number:00}_{d.Id}.asset";
                var b = AssetDatabase.LoadAssetAtPath<BlessingCardDefinition>(p);
                if (b == null) { b = ScriptableObject.CreateInstance<BlessingCardDefinition>(); AssetDatabase.CreateAsset(b, p); }
                artMap.TryGetValue(d.Id, out string path);
                var sprite = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
                b.Configure(d, sprite);
                EditorUtility.SetDirty(b);
                list.Add(b);
            }
            return list;
        }

        private static CampaignDefinition GenerateCampaign(List<FloorSpec> floors, List<BattleSpec> battles, List<RunePoolSpec> pool)
        {
            string p = $"{RootDir}/Campaign/Campaign_Default.asset";
            var c = AssetDatabase.LoadAssetAtPath<CampaignDefinition>(p);
            if (c == null) { c = ScriptableObject.CreateInstance<CampaignDefinition>(); AssetDatabase.CreateAsset(c, p); }
            c.Configure(floors, battles, pool);
            EditorUtility.SetDirty(c);
            return c;
        }
    }
}
