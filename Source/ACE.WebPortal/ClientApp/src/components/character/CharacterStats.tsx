import { useState, useEffect } from 'react'
import { Activity } from 'lucide-react'
import { api } from '../../services/api'
import { StatsData } from '../../types'
import AttributeSection from './AttributeSection'
import VitalSection from './VitalSection'
import RatingSection from './RatingSection'
import ProgressionHeader from './ProgressionHeader'
import BankSection from './BankSection'
import AugmentationSection from './AugmentationSection'

interface CharacterStatsProps {
  guid: number
}

export default function CharacterStats({ guid }: CharacterStatsProps) {
  const [stats, setStats] = useState<StatsData | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let isAborted = false
    
    const fetchStats = async () => {
      try {
        setError(null)
        setStats(null)
        setIsLoading(true)
        
        const data = await api.get<StatsData>(`/api/character/stats/${guid}`)
        
        if (!isAborted) {
          setStats(data)
        }
      } catch (err) {
        if (!isAborted) {
          setError(err instanceof Error ? err.message : 'Unknown error')
        }
      } finally {
        if (!isAborted) {
          setIsLoading(false)
        }
      }
    }

    fetchStats()

    return () => {
      isAborted = true
    }
  }, [guid])

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px]">
        <Activity className="w-8 h-8 text-blue-500 animate-spin opacity-50" />
      </div>
    )
  }

  if (error || !stats) {
    return (
      <div className="p-8 text-center text-red-400 font-medium bg-neutral-950/20 border border-red-500/10 rounded-2xl">
        {error || 'Stats not available'}
      </div>
    )
  }

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 max-w-6xl mx-auto">
      <div className="space-y-8">
        <ProgressionHeader level={stats.level} enlightenment={stats.enlightenment} />
        <AttributeSection attributes={stats.attributes} />
        <VitalSection vitals={stats.vitals} />
      </div>

      <div className="space-y-8">
        <BankSection bank={stats.bank} />
        <AugmentationSection augmentations={stats.augmentations} />
        <RatingSection ratings={stats.ratings} />
      </div>
    </div>
  )
}
