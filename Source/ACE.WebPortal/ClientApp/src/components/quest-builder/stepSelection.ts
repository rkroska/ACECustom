import type { QuestFlow } from '../../types/questBuilder'

/** Stable id for a step in the flow editor / canvas. */
export type StepKey =
  | 'trigger'
  | `main:${number}`
  | `inq:${number}:cooldown:${number}`
  | `inq:${number}:complete:${number}`

export function parseStepKey(key: string): {
  kind: 'trigger' | 'main' | 'branch'
  mainIndex?: number
  branch?: 'onCooldown' | 'canComplete'
  branchIndex?: number
} | null {
  if (key === 'trigger') return { kind: 'trigger' }
  const main = /^main:(\d+)$/.exec(key)
  if (main) return { kind: 'main', mainIndex: Number(main[1]) }
  const br = /^inq:(\d+):(cooldown|complete):(\d+)$/.exec(key)
  if (br) {
    return {
      kind: 'branch',
      mainIndex: Number(br[1]),
      branch: br[2] === 'cooldown' ? 'onCooldown' : 'canComplete',
      branchIndex: Number(br[3]),
    }
  }
  return null
}

export function mainStepKey(index: number): StepKey {
  return `main:${index}`
}

export function branchStepKey(
  inqIndex: number,
  branch: 'onCooldown' | 'canComplete',
  branchIndex: number
): StepKey {
  const seg = branch === 'onCooldown' ? 'cooldown' : 'complete'
  return `inq:${inqIndex}:${seg}:${branchIndex}`
}

export function allStepKeys(flow: QuestFlow): StepKey[] {
  const keys: StepKey[] = ['trigger']
  flow.steps.forEach((step, i) => {
    keys.push(mainStepKey(i))
    if (step.type === 'InqQuest' && step.branches) {
      step.branches.onCooldown?.forEach((_, bi) => keys.push(branchStepKey(i, 'onCooldown', bi)))
      step.branches.canComplete?.forEach((_, bi) => keys.push(branchStepKey(i, 'canComplete', bi)))
    }
  })
  return keys
}
