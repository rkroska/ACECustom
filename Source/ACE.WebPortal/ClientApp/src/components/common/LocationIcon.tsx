import React from 'react';
import { Gem, Sun, MapPin, Compass } from 'lucide-react';

interface LocationIconProps {
  categoryOrdinal: number;
  variation: number | null;
  className?: string;
}

const LocationIcon: React.FC<LocationIconProps> = ({ categoryOrdinal, variation, className = "" }) => {
  // Category 1: Special Locations
  if (categoryOrdinal === 1) {
    return (
      <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all bg-emerald-500/10 border border-emerald-500/20 text-emerald-500 ${className}`}>
        <MapPin className="w-4 h-4" />
      </div>
    );
  }

  // Category 2: Outdoors
  if (categoryOrdinal === 2) {
    return (
      <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all bg-amber-500/10 border border-amber-500/20 text-amber-500 ${className}`}>
        <Sun className="w-4 h-4" />
      </div>
    );
  }

  // Category 3: Dungeons
  if (categoryOrdinal === 3) {
    if (variation !== null) {
      // Dungeons with variations
      return (
        <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all bg-violet-500/10 border border-violet-500/20 text-violet-400 ${className}`}>
          <Gem className="w-4 h-4" />
        </div>
      );
    } else {
      // Standard Dungeons
      return (
        <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all bg-blue-500/10 border border-blue-500/20 text-blue-500 ${className}`}>
          <Gem className="w-4 h-4" />
        </div>
      );
    }
  }

  // Fallback: Other categories
  return (
    <div className={`w-8 h-8 rounded-lg flex items-center justify-center transition-all bg-neutral-500/10 border border-neutral-800 text-neutral-500 ${className}`}>
      <Compass className="w-4 h-4" />
    </div>
  );
};

export default LocationIcon;
