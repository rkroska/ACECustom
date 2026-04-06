import { useState, useEffect, useCallback } from 'react'
import { Star, Activity, Search, ShieldAlert } from 'lucide-react'
import { api } from '../../services/api'
import { SkillData } from '../../types'
import SkillListGroup from './SkillListGroup'

interface CharacterSkillsProps {
  guid: number
}

export default function CharacterSkills({ guid }: CharacterSkillsProps) {
  const [skills, setSkills] = useState<SkillData[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [searchTerm, setSearchTerm] = useState('')

  const fetchSkills = useCallback(async () => {
    try {
      setIsLoading(true)
      setError(null)
      const data = await api.get<SkillData[]>(`/api/character/skills/${guid}`)
      setSkills(data ?? [])
    } catch (err) {
      console.error(err)
      setError('Failed to load skills. Please check your connection and try again.')
    } finally {
      setIsLoading(false)
    }
  }, [guid])

  useEffect(() => {
    fetchSkills()
  }, [fetchSkills])

  const filteredSkills = skills.filter(s => 
    s.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
    s.sac.toLowerCase().includes(searchTerm.toLowerCase())
  )

  const specialized = filteredSkills.filter(s => s.sac === 'Specialized' && s.isUsable)
  const trained = filteredSkills.filter(s => s.sac === 'Trained' && s.isUsable)
  const untrained = filteredSkills.filter(s => (s.sac === 'Untrained' || s.sac === 'None') && s.isUsable)
  const unusable = filteredSkills.filter(s => !s.isUsable)

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <Activity className="w-8 h-8 text-blue-500 animate-spin opacity-50" />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[400px] text-center p-8 bg-neutral-950/20 border border-neutral-800/40 rounded-3xl">
        <ShieldAlert className="w-12 h-12 text-red-500 mb-4 opacity-50" />
        <p className="text-neutral-400 font-medium mb-6 max-w-xs">{error}</p>
        <button 
          onClick={() => fetchSkills()}
          className="px-6 py-2 bg-neutral-800 hover:bg-neutral-700 text-white text-xs font-bold uppercase tracking-widest rounded-xl transition-all"
        >
          Retry Load
        </button>
      </div>
    )
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div className="flex items-center justify-between gap-4 mb-4">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-neutral-500" />
          <input 
            type="text"
            placeholder="Search skills..."
            aria-label="Search skills"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full bg-neutral-950 border border-neutral-800 rounded-xl py-2 pl-10 pr-4 text-sm text-white placeholder-neutral-600 focus:outline-none focus:border-blue-500/50 transition-all font-medium"
          />
        </div>
      </div>

      <div className="bg-neutral-950/30 border border-neutral-800/50 rounded-2xl shadow-2xl backdrop-blur-sm overflow-hidden">
        <div className="divide-y divide-neutral-800/20">
          {specialized.length > 0 && <SkillListGroup title="Specialized Skills" skills={specialized} category="specialized" />}
          {trained.length > 0 && <SkillListGroup title="Trained Skills" skills={trained} category="trained" />}
          {untrained.length > 0 && <SkillListGroup title="Untrained Skills" skills={untrained} category="untrained" />}
          {unusable.length > 0 && <SkillListGroup title="Unusable Skills" skills={unusable} category="unusable" />}
        </div>

        {filteredSkills.length === 0 && (
          <div className="text-center py-16 px-4">
            <Star className="w-12 h-12 mx-auto mb-4 text-neutral-800" />
            <p className="text-neutral-500 uppercase tracking-widest text-[10px] font-bold">No skills found matching your search</p>
          </div>
        )}
      </div>
    </div>
  )
}
