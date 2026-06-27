export interface PatchNotesMeta {
  publicUrl: string
  lastUpdatedAt: string | null
}

export interface PatchNotePublic {
  id: number
  slug: string
  title: string
  summary: string | null
  body: string
  publishedAt: string | null
  updatedAt: string
  publicUrl: string
}

export interface PatchNoteAdmin extends PatchNotePublic {
  status: 'draft' | 'published'
  publishedByAccountId: number | null
  postToDiscord: boolean
  discordMessageId: number | null
  createdAt: string
}

export interface PatchNotesPaged<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface PatchNoteWrite {
  title: string
  slug?: string
  summary?: string
  body: string
  postToDiscord: boolean
}

export interface PatchNotesDiscordResult {
  status: 'not_requested' | 'sent' | 'skipped' | 'failed'
  message: string
  messageId?: number | null
}

export interface PatchNotePublishResponse {
  note: PatchNoteAdmin
  discord: PatchNotesDiscordResult
}
