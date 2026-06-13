import { PatchNoteWrite } from '../types/patchNotes'

export function buildPatchNotesAiPrompt(draft: PatchNoteWrite, publicUrlHint?: string): string {
  const urlLine = publicUrlHint ? `\nPublic URL (when published): ${publicUrlHint}` : ''
  return `You are helping write patch notes for an Asheron's Call private shard.

Title: ${draft.title || '(untitled)'}
Summary (for list + Discord): ${draft.summary?.trim() || '(none yet)'}
${urlLine}

Draft body (Markdown):
---
${draft.body || '(empty)'}
---

Please:
1. Polish the summary (1-2 sentences, player-friendly).
2. Improve the body for clarity; keep Markdown (headings, lists, tables if useful).
3. Suggest a concise Discord-friendly one-liner if the summary is long.
Do not invent features that are not in the draft.`
}
