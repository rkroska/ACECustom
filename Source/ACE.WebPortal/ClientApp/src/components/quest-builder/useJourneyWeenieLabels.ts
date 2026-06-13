import { useEffect, useState } from 'react'
import type { QuestJourney } from '../../types/questBuilder'
import { collectResolvableWcids, getCachedWeenieLabel, resolveWeenieLabels, seedJourneyLabels } from './weenieLookup'

function nameSeedKey(journey: QuestJourney): string {
  const src = journey.obtainItem.source
  const srcName = src.kind === 'corpse' ? src.creature.name : src.objectName
  const pickup = journey.meta.pickupStamp?.name ?? ''
  return `${journey.obtainItem.itemName}|${srcName}|${journey.start.npcName}|${journey.meta.completionStamp.name}|${pickup}`
}

export function useJourneyWeenieLabels(journey: QuestJourney | null) {
  const [labels, setLabels] = useState<Record<number, string>>({})

  const wcidKey = journey
    ? collectResolvableWcids(journey)
        .sort((a, b) => a - b)
        .join(',')
    : ''
  const seedKey = journey ? nameSeedKey(journey) : ''

  useEffect(() => {
    if (!journey) {
      setLabels({})
      return
    }

    seedJourneyLabels(journey)
    const wcids = collectResolvableWcids(journey)
    const cached: Record<number, string> = {}
    for (const id of wcids) {
      const name = getCachedWeenieLabel(id)
      if (name) cached[id] = name
    }
    if (Object.keys(cached).length > 0) {
      setLabels((prev) => ({ ...prev, ...cached }))
    }

    let cancelled = false

    resolveWeenieLabels(wcids).then((resolved) => {
      if (!cancelled) {
        setLabels((prev) => ({ ...prev, ...resolved }))
      }
    })

    return () => {
      cancelled = true
    }
  }, [wcidKey, seedKey]) // eslint-disable-line react-hooks/exhaustive-deps -- journey read when wcid/name keys change

  return labels
}
