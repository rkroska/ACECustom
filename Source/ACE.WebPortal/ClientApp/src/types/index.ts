// --- Common Entity Types ---

export interface WorldLocation {
  landblock: number;
  variation: number | null;
  coordinates: string;
  isDungeon: boolean;
  name: string;
  categoryName: string;
  categoryOrdinal: number;
}

export interface Character {
  guid: number;
  name: string;
  wcid: number;
  isOnline: boolean;
  isAdmin?: boolean;
  location?: WorldLocation | null;
}

// --- Stats & Progression ---

export interface StatDetail {
  innate: number;
  ranks: number;
  total: number;
}

export interface StatsData {
  attributes: {
    strength: StatDetail;
    endurance: StatDetail;
    quickness: StatDetail;
    coordination: StatDetail;
    focus: StatDetail;
    self: StatDetail;
  };
  vitals: {
    health: StatDetail;
    stamina: StatDetail;
    mana: StatDetail;
  };
  ratings: {
    emd: number | null;
    dr: number | null;
    cdr: number | null;
    damage: number | null;
    critDamage: number | null;
  };
  augmentations: Record<string, number>;
  bank: Record<string, number>;
  level: number;
  enlightenment: number;
}

// --- Inventory & Items ---

export interface InventoryItem {
  guid: number;
  containerGuid: number;
  weenieType: number;
  requiresBackpackSlot: boolean;
  isContainer: boolean;
  name: string;
  wcid: number;
  stackSize: number;
  isEquipped: boolean;
  iconId?: number;
  iconUnderlayId?: number;
  iconOverlayId?: number;
  iconOverlaySecondaryId?: number;
  itemType?: number;
  uiEffects?: number;
}

// --- Property & Server Management ---

export interface PropertyMetadata {
  id: number;
  name: string;
  type: string;
  linkedEnum?: string | null;
}

export interface ServerParamMetadata {
  name: string;
  type: string;
  description: string;
  defaultValue: string;
  currentValue: string;
  isSet: boolean;
}

export interface ItemSearchResult {
  wcid: number;
  name: string;
  className: string;
  weenieType: string;
}

export interface ItemReferenceResult extends ItemSearchResult {
  worldReferences?: WorldItemReferences;
  shard?: ShardItemReferences;
}

export interface WorldItemReferences {
  createListCount: number;
  generatorCount: number;
  landblockInstanceCount: number;
  createList: WorldCreateListRef[];
  generators: WorldGeneratorRef[];
  landblockInstances: WorldLandblockRef[];
}

export interface WorldCreateListRef {
  parentWcid: number;
  parentClassName: string;
  parentName: string;
  destinationType: string;
  stackSize: number;
}

export interface WorldGeneratorRef {
  parentWcid: number;
  parentClassName: string;
  parentName: string;
  probability: number;
  maxCreate: number;
  stackSize?: number | null;
}

export interface WorldLandblockRef {
  guid: number;
  landblock?: number | null;
  landblockHex?: string | null;
  objCellId: string;
}

export interface ShardItemReferences {
  totalCount: number;
  limit: number;
  offset: number;
  instances: ShardItemInstance[];
}

export interface StampLookupResult {
  stampName: string;
  serverTotalCompletions: number;
  accountHolderCount: number;
  characterHolderCount: number;
  limit: number;
  offset: number;
  accountHolders: AccountStampHolder[];
  characterHolders: CharacterStampHolder[];
}

export interface AccountStampHolder {
  accountId: number;
  accountName: string;
  numTimesCompleted: number;
}

export interface CharacterStampHolder {
  characterId: number;
  characterName: string;
  accountId: number;
  numTimesCompleted: number;
  lastTimeCompletedUnix: number;
  lastTimeCompletedUtc?: string | null;
}

export interface ShardItemInstance {
  biotaId: number;
  biotaHex: string;
  weenieType: string;
  stackSize: number;
  itemName?: string | null;
  containerId?: number | null;
  containerHex?: string | null;
  locationKind: string;
  ownerName?: string | null;
  ownerGuid?: number | null;
  locationDetail: string;
  characterLinkGuid?: number | null;
}

export interface ServerEventMetadata {
  name: string;
  state: string;
  isActive: boolean;
  canStart: boolean;
  canStop: boolean;
  isDisabled: boolean;
  isScheduled: boolean;
  startTime: number;
  endTime: number;
  startCommand: string;
  stopCommand: string;
  statusCommand: string;
  isVirtual: boolean;
}

export interface SkillData {
  name: string;
  sac: string;
  base: number;
  total: number | null;
  isUsable: boolean;
}

// --- Auth & API ---

export interface AuthUser {
  username: string;
  token: string;
  accessLevel: number;
  lastLogin: string;
}

export interface ApiError {
  message: string;
  code?: string;
  details?: any;
}

// --- Lookup Tables & Enum Metadata ---

export interface EnumListItem {
  name: string
  isLinked: boolean
}

export interface EnumValueMetadata {
  id: number | string
  name: string
  hexValue?: string | null
}

export interface EnumDetail {
  isFlags: boolean
  underlyingType: string
  values: EnumValueMetadata[]
  primaryProperty?: string | null
}
