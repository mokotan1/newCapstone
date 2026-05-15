import fs from "node:fs/promises";
import path from "node:path";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const root = process.cwd();
const sourcePath = path.join(root, "the_unholy_final_scenario.txt");
const outputPath = path.join(root, "the_unholy_parsing_sheet.xlsx");

const speakerMap = new Map([
  ["TV 앵커", { id: "tv_anchor", ko: "TV 앵커", en: "TV Anchor", ja: "" }],
  ["주인공", { id: "player", ko: "주인공", en: "Detective", ja: "" }],
  ["조수", { id: "assistant", ko: "조수", en: "Assistant", ja: "" }],
  ["의뢰인", { id: "client", ko: "의뢰인", en: "Client", ja: "" }],
  ["체셔", { id: "cheshire", ko: "체셔", en: "Cheshire", ja: "" }],
  ["알프레드", { id: "alfred", ko: "알프레드", en: "Alfred", ja: "" }],
  ["알 수 없는 희생자", { id: "unknown_victim", ko: "알 수 없는 희생자", en: "Unknown Victim", ja: "" }],
  ["피투성이 사내", { id: "bloodied_man", ko: "피투성이 사내", en: "Bloodied Man", ja: "" }],
  ["아들의 쪽지", { id: "son_note", ko: "아들의 쪽지", en: "Son's Note", ja: "" }],
]);

const narrator = { id: "narrator", ko: "서술", en: "Narration", ja: "" };
const sceneBlocks = new Map([
  [3, { id: "prologue_butchery_room", title: "프롤로그: 도축장 같은 방" }],
  [4, { id: "opening_office_start", title: "1막: 의뢰와 실종" }],
  [5, { id: "mansion_entry_start", title: "2막: 저택 진입과 첫 번째 단서" }],
  [6, { id: "maid_room_study", title: "2막 후반: 가정부 방과 서재" }],
  [7, { id: "second_floor_clues", title: "3막 전반: 2층과 가족의 흔적" }],
  [8, { id: "master_bedroom_truth", title: "3막 후반: 안방과 진실" }],
  [9, { id: "basement_laboratory", title: "마지막 장: 지하 연구실" }],
  [10, { id: "final_confrontation", title: "최종 대치" }],
  [11, { id: "ending_escape", title: "엔딩" }],
]);

const knownSections = new Set([
  "장면 설명",
  "대사",
  "주요 대사",
  "전환",
  "플레이 목표",
  "진행",
  "단서와 연출",
  "가정부 방",
  "서재",
  "2층으로 이동",
  "딸의 방",
  "아들의 방",
  "알프레드의 일기 요약",
  "감옥",
  "연구 자료",
  "전투 연출",
  "마지막 내레이션",
]);

function normalizeText(text) {
  return text.replace(/^\uFEFF/, "").trim();
}

function commandFor(section) {
  if (section === "플레이 목표") return "objective";
  if (section === "진행") return "flow_note";
  if (section === "단서와 연출" || section === "전투 연출") return "direction_note";
  if (section.includes("요약") || section === "연구 자료") return "lore_note";
  return "talk_standing";
}

function quoteText(text) {
  const match = text.match(/^([^:：]+)[:：]\s*[“"](.*)[”"]$/);
  if (!match) return null;
  const speakerName = match[1].trim();
  const speaker = speakerMap.get(speakerName);
  if (!speaker) return null;
  return { speaker, text: match[2].trim() };
}

function slugLineId(blockId, index) {
  return `${blockId.toUpperCase()}_${String(index).padStart(3, "0")}`;
}

function parseScenario(text) {
  const lines = text
    .split(/\r?\n/)
    .map(normalizeText)
    .filter(Boolean);

  const rows = [];
  const notes = [];
  const blockCounts = new Map();
  let sceneNo = null;
  let sceneTitle = "";
  let section = "";

  for (const line of lines) {
    const heading = line.match(/^(\d+)\.\s+(.+)$/);
    if (heading) {
      sceneNo = Number(heading[1]);
      sceneTitle = heading[2].trim();
      section = "";
      continue;
    }

    if (knownSections.has(line)) {
      section = line;
      continue;
    }

    const block = sceneBlocks.get(sceneNo);
    if (!block) {
      notes.push([sceneNo ?? "", sceneTitle, section, line]);
      continue;
    }

    const parsedDialogue = quoteText(line);
    const speaker = parsedDialogue?.speaker ?? narrator;
    const ko = parsedDialogue?.text ?? line;
    const command = parsedDialogue ? "talk_standing" : commandFor(section);
    const currentCount = (blockCounts.get(block.id) ?? 0) + 1;
    blockCounts.set(block.id, currentCount);

    rows.push([
      slugLineId(block.id, currentCount),
      sceneNo,
      block.title,
      block.id,
      currentCount,
      section || "본문",
      command,
      speaker.id,
      speaker.ko,
      ko,
      "",
      "",
      parsedDialogue ? "dialogue" : "narration_or_note",
      "Google Doc",
    ]);
  }

  return { rows, notes, blockCounts };
}

function colName(index) {
  let name = "";
  let number = index + 1;
  while (number > 0) {
    const mod = (number - 1) % 26;
    name = String.fromCharCode(65 + mod) + name;
    number = Math.floor((number - mod) / 26);
  }
  return name;
}

function writeSheet(sheet, matrix) {
  const rows = matrix.length;
  const cols = matrix[0].length;
  sheet.getRange(`A1:${colName(cols - 1)}${rows}`).values = matrix;
  sheet.freezePanes.freezeRows(1);
  sheet.getRange(`A1:${colName(cols - 1)}1`).format = {
    fill: "#1F4E79",
    font: { color: "#FFFFFF", bold: true },
    horizontalAlignment: "center",
    verticalAlignment: "center",
    wrapText: true,
  };
  sheet.getRange(`A1:${colName(cols - 1)}${rows}`).format = {
    borders: { preset: "all", style: "thin", color: "#D9E2F3" },
    verticalAlignment: "top",
    wrapText: true,
  };
  sheet.getRange(`A1:${colName(cols - 1)}1`).format.autofitColumns();
}

async function main() {
  const source = await fs.readFile(sourcePath, "utf8");
  const { rows, notes, blockCounts } = parseScenario(source);
  const workbook = Workbook.create();

  const parser = workbook.worksheets.getOrAdd("Parser Rows", { renameFirstIfOnlyNewSpreadsheet: true });
  writeSheet(parser, [
    [
      "line_id",
      "scene_no",
      "scene_title",
      "block_id",
      "order_in_block",
      "section",
      "command",
      "speaker_id",
      "speaker_ko",
      "ko",
      "en",
      "ja",
      "row_type",
      "source",
    ],
    ...rows,
  ]);

  const blocks = workbook.worksheets.getOrAdd("Blocks");
  writeSheet(blocks, [
    ["scene_no", "scene_title", "block_id", "rows", "recommended_fungus_command"],
    ...[...sceneBlocks.entries()].map(([sceneNo, block]) => [
      sceneNo,
      block.title,
      block.id,
      blockCounts.get(block.id) ?? 0,
      "PlayScenarioBlockCommand",
    ]),
  ]);

  const speakers = workbook.worksheets.getOrAdd("Speakers");
  const speakerRows = [narrator, ...speakerMap.values()].map((speaker) => [
    speaker.id,
    speaker.ko,
    speaker.en,
    speaker.ja,
  ]);
  writeSheet(speakers, [["speaker_id", "ko", "en", "ja"], ...speakerRows]);

  const noteSheet = workbook.worksheets.getOrAdd("Notes");
  writeSheet(noteSheet, [
    ["scene_no", "scene_title", "section", "ko"],
    ...notes,
  ]);

  const inspect = await workbook.inspect({
    kind: "table",
    range: "Parser Rows!A1:N12",
    include: "values",
    tableMaxRows: 12,
    tableMaxCols: 14,
  });
  console.log(inspect.ndjson);

  const output = await SpreadsheetFile.exportXlsx(workbook);
  await output.save(outputPath);
  console.log(JSON.stringify({ outputPath, parserRows: rows.length, notes: notes.length }));
}

await main();
