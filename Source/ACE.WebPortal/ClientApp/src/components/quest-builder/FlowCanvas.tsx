import { useCallback, useEffect, useMemo } from 'react'
import {
  ReactFlow,
  Background,
  Controls,
  Position,
  type Edge,
  type Node,
  type NodeMouseHandler,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import type { QuestFlow, QuestStep } from '../../types/questBuilder'
import { motionLabel } from './motionPresets'
import {
  branchStepKey,
  mainStepKey,
  type StepKey,
} from './stepSelection'

const STEP_LABELS: Record<string, string> = {
  Tell: 'Tell',
  DirectBroadcast: 'Broadcast',
  Give: 'Give item',
  TakeItems: 'Take item',
  InqQuest: 'Check quest stamp',
  StampQuest: 'Stamp quest',
  Motion: 'Motion',
}

function stepLabel(step: QuestStep) {
  const base = STEP_LABELS[step.type] ?? step.type
  if (step.type === 'Tell' || step.type === 'DirectBroadcast') {
    const t = step.text?.trim()
    return t ? `${base}\n${t.slice(0, 60)}${t.length > 60 ? '…' : ''}` : base
  }
  if (step.type === 'InqQuest' || step.type === 'StampQuest') return `${base}\n«${step.stamp ?? '?'}»`
  if (step.type === 'Give' || step.type === 'TakeItems') return `${base}\nWCID ${step.wcid ?? '?'} ×${step.stack ?? 1}`
  if (step.type === 'Motion') return `${base}\n${motionLabel(step.motion)}`
  return base
}

type Props = {
  flow: QuestFlow
  actorName: string
  selectedKey: StepKey | null
  onSelectStep: (key: StepKey | null) => void
}

export default function FlowCanvas({ flow, actorName, selectedKey, onSelectStep }: Props) {
  const { nodes, edges } = useMemo(
    () => buildGraph(flow, actorName, selectedKey),
    [flow, actorName, selectedKey]
  )

  const onNodeClick: NodeMouseHandler = useCallback(
    (_, node) => {
      onSelectStep(node.id as StepKey)
    },
    [onSelectStep]
  )

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onSelectStep(null)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onSelectStep])

  return (
    <div className="h-full min-h-[360px] w-full rounded-xl border border-neutral-700 bg-neutral-900/80 overflow-hidden">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodeClick={onNodeClick}
        onPaneClick={() => onSelectStep(null)}
        fitView
        fitViewOptions={{ padding: 0.25 }}
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable
        panOnDrag
        zoomOnScroll
        proOptions={{ hideAttribution: true }}
      >
        <Background color="#333" gap={16} />
        <Controls className="!bg-neutral-800 !border-neutral-600" showInteractive={false} />
      </ReactFlow>
    </div>
  )
}

function buildGraph(
  flow: QuestFlow,
  actorName: string,
  selectedKey: StepKey | null
): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = []
  const edges: Edge[] = []
  const cx = 280
  let y = 0
  let prev: string = 'trigger'

  const addNode = (
    id: StepKey,
    label: string,
    style: { bg: string; border: string; width?: number },
    x = cx
  ) => {
    const selected = selectedKey === id
    nodes.push({
      id,
      position: { x, y },
      data: { label },
      style: nodeStyle(style.bg, selected ? '#93c5fd' : style.border, style.width ?? 220, selected),
      sourcePosition: Position.Bottom,
      targetPosition: Position.Top,
    })
  }

  addNode(
    'trigger',
    `▶ ${flow.trigger}${flow.giveWcid ? `\nRequires item ${flow.giveWcid}` : ''}\n${actorName}`,
    { bg: '#1e3a5f', border: '#3b82f6', width: 260 }
  )
  y += 95

  for (let i = 0; i < flow.steps.length; i++) {
    const step = flow.steps[i]
    const id = mainStepKey(i)

    if (step.type === 'InqQuest' && step.branches) {
      addNode(id, stepLabel(step), { bg: '#422006', border: '#f59e0b', width: 240 })
      edges.push({ id: `e-${prev}-${id}`, source: prev, target: id })
      y += 85

      const cool = step.branches.onCooldown ?? []
      const ok = step.branches.canComplete ?? []
      const branchY = y
      const leftX = cx - 200
      const rightX = cx + 200

      // Hub labels (clickable — selects first branch step or Inq itself)
      const hubL = `inq:${i}:hub:cooldown` as StepKey
      const hubR = `inq:${i}:hub:complete` as StepKey
      nodes.push({
        id: hubL,
        position: { x: leftX, y: branchY },
        data: {
          label: `Stamp active\n(already done)\n${cool.length} step(s)`,
        },
        style: nodeStyle('#3f1d1d', selectedKey === hubL || cool.some((_, bi) => selectedKey === branchStepKey(i, 'onCooldown', bi)) ? '#fca5a5' : '#ef4444', 180, selectedKey?.startsWith(`inq:${i}:cooldown`) ?? false),
        sourcePosition: Position.Bottom,
        targetPosition: Position.Top,
      })
      nodes.push({
        id: hubR,
        position: { x: rightX, y: branchY },
        data: {
          label: `No stamp yet\n(can turn in)\n${ok.length} step(s)`,
        },
        style: nodeStyle('#14532d', selectedKey === hubR || ok.some((_, bi) => selectedKey === branchStepKey(i, 'canComplete', bi)) ? '#86efac' : '#22c55e', 180, selectedKey?.startsWith(`inq:${i}:complete`) ?? false),
        sourcePosition: Position.Bottom,
        targetPosition: Position.Top,
      })
      edges.push({ id: `e-${id}-${hubL}`, source: id, target: hubL, label: 'has stamp', labelStyle: { fill: '#fca5a5', fontSize: 10 } })
      edges.push({ id: `e-${id}-${hubR}`, source: id, target: hubR, label: 'no stamp', labelStyle: { fill: '#86efac', fontSize: 10 } })

      let ly = branchY + 90
      let ry = branchY + 90
      let lastL = hubL
      let lastR = hubR

      cool.forEach((bs, bi) => {
        const bid = branchStepKey(i, 'onCooldown', bi)
        nodes.push({
          id: bid,
          position: { x: leftX, y: ly },
          data: { label: stepLabel(bs) },
          style: nodeStyle('#292524', selectedKey === bid ? '#fca5a5' : '#78716c', 180, selectedKey === bid),
          sourcePosition: Position.Bottom,
          targetPosition: Position.Top,
        })
        edges.push({ id: `e-${lastL}-${bid}`, source: lastL, target: bid })
        lastL = bid
        ly += 72
      })

      ok.forEach((bs, bi) => {
        const bid = branchStepKey(i, 'canComplete', bi)
        nodes.push({
          id: bid,
          position: { x: rightX, y: ry },
          data: { label: stepLabel(bs) },
          style: nodeStyle('#292524', selectedKey === bid ? '#86efac' : '#78716c', 180, selectedKey === bid),
          sourcePosition: Position.Bottom,
          targetPosition: Position.Top,
        })
        edges.push({ id: `e-${lastR}-${bid}`, source: lastR, target: bid })
        lastR = bid
        ry += 72
      })

      y = Math.max(ly, ry) + 20
      prev = lastR
      continue
    }

    addNode(id, stepLabel(step), { bg: '#1e293b', border: '#64748b' })
    edges.push({ id: `e-${prev}-${id}`, source: prev, target: id })
    y += 78
    prev = id
  }

  return { nodes, edges }
}

function nodeStyle(bg: string, border: string, width: number, selected: boolean) {
  return {
    background: bg,
    color: '#f5f5f5',
    border: `${selected ? 2 : 1}px solid ${border}`,
    borderRadius: 8,
    padding: 10,
    fontSize: 11,
    width,
    whiteSpace: 'pre-wrap' as const,
    cursor: 'pointer',
    boxShadow: selected ? `0 0 12px ${border}55` : undefined,
  }
}
