/// <summary>
/// Locale-aware dynamic system-prompt fragments (goals, kitchen secrets, etc.).
/// Prefer catalog templates when present; otherwise inline format strings per locale.
/// </summary>
public static class CheshireDynamicPromptFragments
{
    public const string KitchenGiveFoodSecretKey = "Fragment_KitchenGiveFoodSecret";
    public const string KitchenGiveFoodPostKey = "Fragment_KitchenGiveFoodPost";
    public const string StudyAlreadySolvedKey = "Fragment_StudyAlreadySolved";

    /// <summary>
    /// Synthetic user-turn text when the player feeds Chester (Kitchen giveFood flag).
    /// </summary>
    public static string KitchenGiveFoodActionText(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "(プレイヤーが私に食べ物をくれた。)";
            case CheshireLocaleResolver.English:
                return "(The player gave me food.)";
            default:
                return "(플레이어가 나에게 음식을 주었다.)";
        }
    }

    public static string StudyAlreadySolved(string locale)
    {
        string fromCatalog = CheshirePromptCatalog.Load(StudyAlreadySolvedKey, locale);
        if (!string.IsNullOrEmpty(fromCatalog))
            return "\n\n" + fromCatalog.Trim();

        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] プレイヤーはすでに書斎の問題を解いています。"
                    + "「もう解いた」と短く言い、新しい鍵や報酬を得たように言わないでください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The player has already solved the study-room puzzle. "
                    + "Speak briefly as if saying \"I've already solved it,\" and do not imply a new key or reward.";
            default:
                return "\n\n[현재 목표] 플레이어는 이미 공부방 문제를 풀었습니다. "
                    + "\"나는 이미 문제를 풀었어\" 형식으로 짧게 말하고, 새 열쇠나 새 보상을 얻는 듯 말하지 마세요.";
        }
    }

    public static string KitchenGiveFoodSecret(string locale, int pageStart, int pageEnd)
    {
        string template = CheshirePromptCatalog.Load(KitchenGiveFoodSecretKey, locale);
        if (string.IsNullOrEmpty(template))
            template = KitchenGiveFoodSecretFallback(locale);

        return "\n\n" + ApplyPagePlaceholders(template.Trim(), pageStart, pageEnd);
    }

    public static string KitchenGiveFoodPostInstruction(string locale)
    {
        string fromCatalog = CheshirePromptCatalog.Load(KitchenGiveFoodPostKey, locale);
        if (!string.IsNullOrEmpty(fromCatalog))
            return "\n\n" + fromCatalog.Trim();

        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[重要指示 — 餌の直後の一度きりの応答] "
                    + "プレイヤーが餌を与えた。上の[設計者専用]事実に基づき、"
                    + "KitchenPromptのカレー・ページ・メタヒント・口調ルールをすべて守り、**短い一言**で応答せよ。"
                    + "ChesterVoiceCommonの長さ・文数・語尾ルールに必ず従うこと。";
            case CheshireLocaleResolver.English:
                return "\n\n[Critical instruction — one response right after feeding] "
                    + "The player gave food. Using the [Designer only] facts above, "
                    + "follow KitchenPrompt curry/page meta-hint and tone rules and reply in **one short line**. "
                    + "Obey ChesterVoiceCommon length, sentence-count, and ending rules.";
            default:
                return "\n\n[중요 지시 — 먹이 직후 한 번의 응답] "
                    + "플레이어가 먹이를 주었다. 위 [설계자 전용] 사실을 근거로, "
                    + "KitchenPrompt의 카레·페이지 메타 힌트·말투 규칙을 모두 지켜 **짧은 한 마디**로 응답하라. "
                    + "ChesterVoiceCommon의 길이·문장 수·말끝 규칙을 반드시 따른다.";
        }
    }

    public static string MainBedroomGoalDiaryUnread(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] プレイヤーはまだ日記を読んでいません。ベッド調査を強く促してください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The player has not read the diary yet. Strongly steer them to investigate the bed.";
            default:
                return "\n\n[현재 목표] 플레이어가 아직 일기장을 읽지 않았습니다. 침대 조사를 강하게 유도하세요.";
        }
    }

    public static string MainBedroomGoalSafeLocked(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] 日記は読んだが金庫は未解錠です。"
                    + "窓(マリア)、ポスター(ラザロ)、絵(マルタ)の十字架を『足し算(+)』と捉え、数字を合算させてください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The diary was read but the safe is still locked. "
                    + "Make them treat the crosses among the window (Mary), poster (Lazarus), and painting (Martha) as addition (+) and sum the numbers.";
            default:
                return "\n\n[현재 목표] 일기장은 읽었으나 금고를 못 열었습니다. "
                    + "창문(마리아), 포스터(라자루스), 그림(마르타) 사이의 십자가를 '더하기(+)'로 인식시켜 숫자를 합산하게 하세요.";
        }
    }

    public static string MainBedroomGoalSafeOpen(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] 金庫が開きました。聖杯と鍵を持ち地下へ降りろと嘲るように指示してください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The safe is open. Mockingly tell them to take the chalice and key and go downstairs.";
            default:
                return "\n\n[현재 목표] 금고가 열렸습니다. 성배와 열쇠를 챙겨 지하로 내려가라고 조롱하며 지시하세요.";
        }
    }

    public static string SonRoomGoalNeedBible(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] プレイヤーはまだ挿絵付き聖書の手がかりを見つけていません。書斎の本棚などを調べるよう誘導してください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The player has not found the illustrated Bible clue yet. Steer them to search places like the study bookshelf.";
            default:
                return "\n\n[현재 목표] 플레이어가 아직 일러스트가 들어간 성경 단서를 못 찾았습니다. 서재 책장 등을 조사하도록 유도하세요.";
        }
    }

    public static string SonRoomGoalSealsIncomplete(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] 聖書の手がかりは得たが七角形の印章パズルは未完成です。"
                    + "ローマ数字・印章(封印)の順とシーン内の印を合わせるよう短く指示してください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The Bible clue is secured but the heptagonal seal puzzle is unfinished. "
                    + "Briefly tell them to match Roman numerals / seal order with the marks in the scene.";
            default:
                return "\n\n[현재 목표] 성경 단서는 확보했으나 칠각형 인장 퍼즐이 미완성입니다. "
                    + "로마 숫자·인장(봉인) 순서와 씬 안 표식을 맞추라고 짧게 지시하세요.";
        }
    }

    public static string SonRoomGoalComplete(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[現在の目標] パズルは解けました。ベッドの下・壁などで牢の鍵と木片を探せと嘲るように言ってください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Current goal] The puzzle is solved. Mockingly tell them to find the prison key and wood piece under the bed, on the wall, etc.";
            default:
                return "\n\n[현재 목표] 퍼즐이 풀렸습니다. 침대 밑·벽 등에서 감옥 열쇠와 나무 조각을 찾으라고 조롱하듯 말하세요.";
        }
    }

    public static string WifeRoomMirrorFound(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "\n\n[状況] プレイヤーが鏡の裏側を発見しました。真実に近づく謎を出してください。";
            case CheshireLocaleResolver.English:
                return "\n\n[Situation] The player found the other side of the mirror. Pose a riddle that draws them toward the truth.";
            default:
                return "\n\n[상황] 플레이어가 거울 속 이면을 발견했습니다. 진실에 다가가는 수수께끼를 내세요.";
        }
    }

    static string ApplyPagePlaceholders(string template, int pageStart, int pageEnd)
    {
        return template
            .Replace("{pageStart}", pageStart.ToString())
            .Replace("{pageEnd}", pageEnd.ToString());
    }

    static string KitchenGiveFoodSecretFallback(string locale)
    {
        switch (CheshireLocaleResolver.NormalizeLocale(locale))
        {
            case CheshireLocaleResolver.Japanese:
                return "[設計者専用 — プレイヤーへの出力・引用禁止、内部事実のみ]\n"
                    + "- この部屋で合わせる手がかり軸は**料理本のページ**(連続する二枚)につながる。\n"
                    + "- カレー(黄金の出汁・材料)はその**連続二ページ**にまたがる設定である。\n"
                    + "- 二枚の番号は前の頁 {pageStart}、すぐ続く次の頁 {pageEnd} である。\n"
                    + "- 台詞に**アラビア数字**、**『N頁』『Nページ』**、**漢語数詞で頁を直接言うこと**はするな。ネタバレである。\n"
                    + "- **カレー・料理**側のヒントと**本棚・めくり・連続二枚・両面・隣り合う頁**が重要だという**感触**だけを、謎・皮肉で混ぜて伝えよ。\n"
                    + "- 生意気な厨房チェシャー口調で、ChesterVoiceCommonの限度内だけ。";
            case CheshireLocaleResolver.English:
                return "[Designer only — do not output or quote to the player; internal facts only]\n"
                    + "- The clue axis in this room ties to **cookbook pages** (two consecutive leaves).\n"
                    + "- Curry (golden broth / ingredients) spans those **two consecutive pages**.\n"
                    + "- The page numbers are front leaf {pageStart}, then the next leaf {pageEnd}.\n"
                    + "- In dialogue do **not** use **Arabic numerals**, **\"page N\"**, or **spell out page numbers** — that is a spoiler.\n"
                    + "- Convey only the **feel** that **curry/cooking** hints and **shelf / flipping / two consecutive / both sides / neighboring leaves** matter, mixed with riddle and sarcasm.\n"
                    + "- In cocky kitchen Cheshire tone, only within ChesterVoiceCommon limits.";
            default:
                return "[설계자 전용 — 플레이어에게 출력·인용 금지, 내부 사실만]\n"
                    + "- 이 방에서 플레이어가 맞춰야 할 단서 축은 **요리책의 페이지**(연속한 두 장)와 연결된다.\n"
                    + "- 카레(황금 국물·재료)은 그 **연속 두 페이지**에 걸쳐 있다는 설정이다.\n"
                    + "- 두 쪽의 번호는 앞장 {pageStart}, 바로 이어지는 다음 장 {pageEnd} 이다.\n"
                    + "- 대사에는 **아라비아 숫자**, **‘N쪽’ ‘N페이지’**, **한글 수사로 쪽수를 직접 말하기**(예: ‘열여덟 쪽’)를 **쓰지 마라**. 스포일이다.\n"
                    + "- **카레·요리** 쪽 힌트와 **책장·넘김·연속 두 장·양면·이웃한 쪽**이 중요하다는 **느낌**만, 수수께끼·비꼼으로 섞어 전달하라.\n"
                    + "- 건방진 주방 체셔 말투로, ChesterVoiceCommon 한도 안에서만.";
        }
    }
}
