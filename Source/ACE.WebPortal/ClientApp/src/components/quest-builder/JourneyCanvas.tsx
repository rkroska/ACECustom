import { useMemo } from 'react'
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
import type { JourneyPhaseId, QuestJourney } from '../../types/questBuilder'
import { journeyPhaseSummary } from './questJourney'

type Props = {
  journey: QuestJourney
  activePhase: JourneyPhaseId
  onSelectPhase: (phase: JourneyPhaseId) => void
}

export default function JourneyCanvas({ journey, activePhase, onSelectPhase }: Props) {
  const { nodes, edges } = useMemo(
    () => buildJourneyGraph(journey, activePhase),
    [journey, activePhase]
  )

  const onNodeClick: NodeMouseHandler = useCallbackNodeClick(onSelectPhase)

  return (
    <div className="h-full min-h-[320px] w-full rounded-xl border border-neutral-700 bg-neutral-900/80 overflow-hidden">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodeClick={onNodeClick}
        fitView
        fitViewOptions={{ padding: 0.3 }}
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

function useCallbackNodeClick(onSelectPhase: (p: JourneyPhaseId) => void): NodeMouseHandler {
  return (_, node) => {
    const id = node.id as JourneyPhaseId
    if (id === 'start' || id === 'obtainItem' || id === 'turnIn') onSelectPhase(id)
  }
}

function buildJourneyGraph(
  journey: QuestJourney,
  activePhase: JourneyPhaseId
): { nodes: Node[]; edges: Edge[] } {
  const phases: { id: JourneyPhaseId; label: string; color: string; border: string }[] = [
    { id: 'start', label: '1. Start\nTalk to NPC', color: '#1e3a5f', border: '#3b82f6' },
    { id: 'obtainItem', label: '2. Obtain item', color: '#422006', border: '#f59e0b' },
    { id: 'turnIn', label: '3. Turn in', color: '#14532d', border: '#22c55e' },
  ]

  const nodes: Node[] = phases.map((p, i) => {
    const selected = activePhase === p.id
    const summary = journeyPhaseSummary(journey, p.id)
    return {
      id: p.id,
      position: { x: 120, y: i * 130 },
      data: { label: `${p.label}\n\n${summary}` },
      style: {
        background: p.color,
        color: '#f5f5f5',
        border: `${selected ? 2 : 1}px solid ${selected ? '#93c5fd' : p.border}`,
        borderRadius: 10,
        padding: 12,
        fontSize: 11,
        width: 280,
        whiteSpace: 'pre-wrap' as const,
        cursor: 'pointer',
        boxShadow: selected ? `0 0 14px ${p.border}66` : undefined,
      },
      sourcePosition: Position.Bottom,
      targetPosition: Position.Top,
    }
  })

  const edges: Edge[] = [
    { id: 'e-start-obtain', source: 'start', target: 'obtainItem' },
    { id: 'e-obtain-turnin', source: 'obtainItem', target: 'turnIn' },
  ]

  return { nodes, edges }
}
