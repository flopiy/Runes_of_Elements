using System.Collections.Generic;

namespace RunesOfElements.Core
{
    /// <summary>Таблиці контенту з GDD v1.2 (правила v1.1). Генератор ScriptableObject-ів і тести використовують саме їх.</summary>
    public static class DefaultContent
    {
        public const int TowerHpPerFloor = 3;

        public static GameContent Create()
        {
            var c = new GameContent();

            // ---------------- Маги ----------------
            c.Mages.Add(new MageDef { Id = "pyromancer", Name = "Піромант", Element = RuneType.Fire, PassiveAbilityId = "mage_pyro", PassiveName = "Палаюча сила", PassiveText = "Вогняні заклинання коштують на 1 концентрацію менше." });
            c.Mages.Add(new MageDef { Id = "hydromancer", Name = "Гідромант", Element = RuneType.Water, PassiveAbilityId = "mage_hydro", PassiveName = "Водний щит", PassiveText = "Після водного заклинання отримує щит, що поглинає наступну атаку монстра." });
            c.Mages.Add(new MageDef { Id = "electromancer", Name = "Електромант", Element = RuneType.Lightning, PassiveAbilityId = "mage_electro", PassiveName = "Ланцюг блискавки", PassiveText = "Якщо на кубику шкоди блискавкою випадає «6», 3 шкоди перекидаються на іншого монстра." });
            c.Mages.Add(new MageDef { Id = "cryomancer", Name = "Кріомант", Element = RuneType.Ice, PassiveAbilityId = "mage_cryo", PassiveName = "Крижаний полон", PassiveText = "Раз на битву може заморозити монстра — той пропускає атаку в цьому раунді." });

            // ---------------- Монстри рівня 1 ----------------
            M(c, "goblin", "Гоблін розлому", MonsterLevel.Level1, 5, 2, "Парна атака", "Якщо на полі є ще один Гоблін розлому, його атаки завдають +1 шкоди.", "mon_goblin_pair");
            M(c, "skeleton", "Скелет-воїн", MonsterLevel.Level1, 6, 2, "Кістяний захист", "Імунітет до першого модифікатора шкоди.", "mon_skeleton_immunity");
            M(c, "spider", "Печерний павук", MonsterLevel.Level1, 4, 2, "Отруйний укус", "Атакована ціль втрачає 1 концентрацію.", "mon_spider_drain");
            M(c, "flyer", "Летюча тварюка", MonsterLevel.Level1, 5, 2, "Ухильність", "Отримує на 1 менше шкоди від форми «Сфера».", "mon_flying_evasive");
            M(c, "wolf", "Тіньовий вовк", MonsterLevel.Level1, 7, 3, "Регенерація", "Після своєї дії відновлює 1 здоров'я.", "mon_wolf_regen");
            M(c, "rotten_guard", "Розкладений страж", MonsterLevel.Level1, 8, 2, "Невмирущий", "Перше смертельне поранення залишає його з 1 здоров'я.", "mon_undying");
            M(c, "fire_elemental", "Вогняний елементаль", MonsterLevel.Level1, 8, 3, "Палаючий удар", "Після атаки ціль отримує «Опік 1».", "mon_fire_burn");
            M(c, "necromancer", "Некромант", MonsterLevel.Level1, 7, 2, "Поклик мертвих", "При першому отриманні шкоди викликає монстра 1-го рівня на вільну позицію.", "mon_necro_summon");

            // ---------------- Монстри рівня 2 ----------------
            M(c, "thunder_shaman", "Громовий шаман", MonsterLevel.Level2, 9, 3, "Висмоктування", "Атакована ціль втрачає 1 концентрацію.", "mon_shaman_drain");
            M(c, "shadow_assassin", "Тіньовий убивця", MonsterLevel.Level2, 10, 3, "Розрив тіні", "Помираючи, завдає 2 шкоди всім магам.", "mon_assassin_death");
            M(c, "ice_golem", "Крижаний голем", MonsterLevel.Level2, 12, 3, "Крижана оболонка", "Перший удар по ньому за раунд завдає на 2 менше.", "mon_golem_firsthit");
            M(c, "rift_demon", "Демон розлому", MonsterLevel.Level2, 11, 3, "Темна лють", "При здоров'ї 5 і нижче отримує +1 до атаки.", "mon_demon_rage");

            // ---------------- Монстри рівня 3 ----------------
            M(c, "ancient_sentinel", "Древній страж", MonsterLevel.Level3, 17, 2, "Кам'яна броня", "Зменшує кожен отриманий удар на 1.", "mon_stone_armor");
            M(c, "thunder_lord", "Громовий Володар", MonsterLevel.Level3, 15, 2, "Грозовий розряд", "Після атаки випадковий маг втрачає 1 концентрацію.", "mon_thunderlord_drain");
            M(c, "rift_lord", "Володар розлому", MonsterLevel.Level3, 18, 2, "Лють Розлому", "При падінні до 10 здоров'я викликає двох монстрів 1-го рівня.", "mon_riftlord_summon");
            M(c, "void_dragon", "Дракон пустоти", MonsterLevel.Level3, 19, 3, "Подих Пустоти", "Усі маги отримують «Отрута 1» (джерело: Розлом).", "mon_dragon_breath");

            // ---------------- Боси ----------------
            M(c, "boss_chaos_archmage", "Архімаг Хаосу", MonsterLevel.Boss, 28, 4, "Бос-здатності",
                "Викривлена магія: щораунду випадкова стихія дорожча на 1.\nВідбиття: атакуючий маг отримує 1 шкоди.\nХаотичний сплеск: кожен 3-й раунд 2 шкоди випадковому магу.",
                "boss_chaos_warp", "boss_chaos_reflect", "boss_chaos_surge");
            M(c, "boss_ice_queen", "Королева Льоду", MonsterLevel.Boss, 30, 4, "Бос-здатності",
                "Вічний холод: маги починають хід з −1 концентрацією (не нижче 1).\nКрижана броня: перший удар за раунд −2.\nЗамороження: щораунду випадковий маг під «Мовчанням» випадкової стихії.",
                "boss_queen_cold", "boss_queen_armor", "boss_queen_freeze");
            M(c, "boss_ancient_guardian", "Древній Страж", MonsterLevel.Boss, 34, 2, "Бос-здатності",
                "Кам'яна броня: кожен удар −1.\nНепохитність: імунітет до заморозки й приголомшення.\nГнів Стража: при 10 здоров'ї і нижче атака стає 6.",
                "mon_stone_armor", "boss_guardian_steadfast", "boss_guardian_wrath");

            // ---------------- Благословення / Прокляття / Ризикові ----------------
            B(c, 1, "blessing_power_flow", "Потік сили", BlessingKind.Blessing, BlessingTrigger.Passive, "Стартова концентрація кожної битви — 5 замість 4.");
            B(c, 2, "blessing_double_swap", "Подвійний обмін", BlessingKind.Blessing, BlessingTrigger.Passive, "Дозволена зміна до двох рун за хід замість однієї.");
            B(c, 3, "blessing_tempered_will", "Загартована воля", BlessingKind.Blessing, BlessingTrigger.Auto, "Імунітет до першого дебафу, накладеного на власника за битву.");
            B(c, 4, "blessing_thirst_knowledge", "Жага знань", BlessingKind.Blessing, BlessingTrigger.Passive, "Медитація дає +3 замість +2 (у межах ліміту 5).");
            B(c, 5, "blessing_resonance", "Магічний резонанс", BlessingKind.Blessing, BlessingTrigger.Auto, "Раз за раунд: якщо заклинання вразило дві і більше цілі — +1 концентрація.");
            B(c, 6, "blessing_titan_blood", "Кров титана", BlessingKind.Blessing, BlessingTrigger.Passive, "Максимум здоров'я 12; старт кожної битви з 12 здоров'я.");
            B(c, 7, "blessing_sorter_luck", "Щастя сортувальника", BlessingKind.Blessing, BlessingTrigger.Active, "Раз за битву: перекинь кубик шкоди та обери кращий результат.", perBattle: 1);
            B(c, 8, "blessing_mage_hand", "Рука мага", BlessingKind.Blessing, BlessingTrigger.Passive, "Ліміт руки — 7 рун замість 6.");
            B(c, 9, "blessing_elemental_bond", "Стихійний зв'язок", BlessingKind.Blessing, BlessingTrigger.Passive, "Обрана стихія коштує на 1 концентрацію менше.");
            B(c, 10, "blessing_hope_radiance", "Сяйво надії", BlessingKind.Blessing, BlessingTrigger.Auto, "Раз за кампанію: коли здоров'я падає до 0 — лишаєшся з 1.", perCampaign: 1);
            B(c, 11, "curse_magic_fatigue", "Магічна втома", BlessingKind.Curse, BlessingTrigger.Passive, "Перша медитація в кожній битві дає +1 замість +2.");
            B(c, 12, "curse_resonance_crack", "Тріщина в резонансі", BlessingKind.Curse, BlessingTrigger.Passive, "Перше заклинання кожного раунду завдає на 2 менше шкоди.");
            B(c, 13, "curse_vain_thirst", "Марна спрага", BlessingKind.Curse, BlessingTrigger.Passive, "Усе отримане зцілення зменшено на 2 (мінімум 0).");
            B(c, 14, "curse_heavy_hand", "Важка рука", BlessingKind.Curse, BlessingTrigger.Passive, "Кожна зміна руни коштує 1 концентрацію.");
            B(c, 15, "curse_rift_whisper", "Шепіт Розлому", BlessingKind.Curse, BlessingTrigger.Auto, "На початку третьої битви до ворогів додається випадковий монстр 1-го рівня.");
            B(c, 16, "risk_chaos_roulette", "Рулетка хаосу", BlessingKind.Risk, BlessingTrigger.Auto, "Початок раунду, d6: 1–2 → −1 концентрація; 3–4 → нічого; 5–6 → +1.");
            B(c, 17, "risk_double_price", "Подвійна ціна", BlessingKind.Risk, BlessingTrigger.Passive, "Усі заклинання завдають +1 шкоди, але коштують +1 концентрації.");
            B(c, 18, "risk_silence_pact", "Пакт мовчання", BlessingKind.Risk, BlessingTrigger.Passive, "Не можна змінювати руни; +1 концентрація на початку кожного ходу.");
            B(c, 19, "risk_fate_exchange", "Обмін долею", BlessingKind.Risk, BlessingTrigger.Passive, "Кожна битва починається з 7 здоров'я, але заклинання завдають +2 шкоди.");
            B(c, 20, "risk_fortune_mirror", "Дзеркало фортуни", BlessingKind.Risk, BlessingTrigger.Active, "Раз за кампанію: заміни результат свого кидка на протилежний бік (1↔6, 2↔5, 3↔4).", perCampaign: 1);

            // ---------------- Башта новачка: 10 поверхів × 3 хвилі ----------------
            c.Tower.AddRange(CreateNoviceTower());
            // Баланс (симуляція ботами, 150 ігор): +3 HP монстрам за кожен поверх вище першого ≈ 70 % перемог.
            for (int f = 0; f < c.Tower.Count; f++) c.Tower[f].MonsterHpBonus = f * TowerHpPerFloor;
            c.RebuildCampaignFromTower();

            // ---------------- Спільний запас рун (припущення) ----------------
            c.RunePool.Add(new RunePoolSpec { Rune = RuneType.Fire, Count = 6 });
            c.RunePool.Add(new RunePoolSpec { Rune = RuneType.Water, Count = 6 });
            c.RunePool.Add(new RunePoolSpec { Rune = RuneType.Ice, Count = 6 });
            c.RunePool.Add(new RunePoolSpec { Rune = RuneType.Lightning, Count = 6 });
            c.RunePool.Add(new RunePoolSpec { Rune = RuneType.Beam, Count = 6 });
            c.RunePool.Add(new RunePoolSpec { Rune = RuneType.Sphere, Count = 4 });
            return c;
        }

        private static BattleSpec W(int l1 = 0, int l2 = 0, int l3 = 0, bool boss = false) =>
            new BattleSpec { Level1 = l1, Level2 = l2, Level3 = l3, Boss = boss };

        private static FloorSpec F(string name, params BattleSpec[] waves) =>
            new FloorSpec { Name = name, Waves = new List<BattleSpec>(waves) };

        /// <summary>Башта новачка (план: 10 поверхів × 3 хвилі).</summary>
        public static List<FloorSpec> CreateNoviceTower() => new List<FloorSpec>
        {
            F("Поверх 1 — Брама",            W(l1: 2), W(l1: 2), W(l1: 3)),
            F("Поверх 2 — Казарми",          W(l1: 3), W(l1: 3), W(l1: 4)),
            F("Поверх 3 — Бібліотека",       W(l1: 3), W(l1: 2, l2: 1), W(l2: 2)),
            F("Поверх 4 — Склепіння",        W(l2: 2), W(l1: 1, l2: 2), W(l2: 3)),
            F("Поверх 5 — Зала стража",      W(l2: 2), W(l1: 1, l2: 2), W(l1: 2, l3: 1)),
            F("Поверх 6 — Кузня рун",        W(l2: 3), W(l1: 2, l3: 1), W(l2: 1, l3: 1)),
            F("Поверх 7 — Грозова галерея",  W(l2: 1, l3: 1), W(l1: 1, l2: 1, l3: 1), W(l3: 2)),
            F("Поверх 8 — Крижаний ярус",    W(l2: 2, l3: 1), W(l1: 1, l3: 2), W(l2: 1, l3: 2)),
            F("Поверх 9 — Тінь Розлому",     W(l1: 2, l3: 2), W(l2: 1, l3: 2), W(l3: 3)),
            F("Поверх 10 — Обсерваторія",    W(l2: 3), W(l2: 2, l3: 1), W(l1: 2, boss: true)),
        };

        private static void M(GameContent c,string id, string name, MonsterLevel lvl, int hp, int atk, string abilityName, string text, params string[] abilities)
        {
            c.Monsters.Add(new MonsterDef { Id = id, Name = name, Level = lvl, Hp = hp, Attack = atk, AbilityName = abilityName, AbilityText = text, AbilityIds = new List<string>(abilities) });
        }

        private static void B(GameContent c, int n, string id, string name, BlessingKind kind, BlessingTrigger trig, string text, int perBattle = 0, int perCampaign = 0)
        {
            c.Blessings.Add(new BlessingDef { Number = n, Id = id, Name = name, Kind = kind, Trigger = trig, Text = text, UsesPerBattle = perBattle, UsesPerCampaign = perCampaign });
        }
    }
}
