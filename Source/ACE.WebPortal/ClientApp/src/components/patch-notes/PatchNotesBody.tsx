import ReactMarkdown from 'react-markdown'
import remarkGfm from 'remark-gfm'

/** Renders patch note body as Markdown (headings, lists, bold, tables, links, code). */
export default function PatchNotesBody({ body }: { body: string }) {
  return (
    <div className="patch-notes-markdown prose prose-invert prose-neutral max-w-none prose-headings:text-white prose-headings:font-bold prose-a:text-blue-400 prose-a:no-underline hover:prose-a:text-blue-300 prose-strong:text-white prose-code:text-blue-200 prose-code:bg-neutral-900 prose-code:px-1 prose-code:rounded prose-pre:bg-neutral-950 prose-pre:border prose-pre:border-neutral-800 prose-table:text-sm prose-th:text-left prose-td:border-neutral-800 prose-th:border-neutral-800">
      <ReactMarkdown remarkPlugins={[remarkGfm]}>{body}</ReactMarkdown>
    </div>
  )
}
