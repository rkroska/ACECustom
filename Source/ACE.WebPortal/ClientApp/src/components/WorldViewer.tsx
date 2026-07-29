import React, { type FC, useState, useEffect, useRef, Suspense } from 'react';
import { Canvas, useFrame, useThree } from '@react-three/fiber';
import { OrbitControls, useGLTF, Html } from '@react-three/drei';
import * as THREE from 'three';
import { 
  Globe, 
  RotateCw, 
  Palette as PaletteIcon, 
  Play, 
  Pause, 
  Grid,
  Sun,
  Eye,
  RefreshCw,
  Search,
  ChevronRight,
  ChevronUp,
  ChevronDown,
  AlertTriangle,
  Camera,
  Download,
  Layers,
  Sliders,
  ThumbsUp,
  ThumbsDown,
  CheckCircle2,
  XCircle,
  MessageSquare,
  Copy,
  RotateCcw,
  Send
} from 'lucide-react';

// --- Error Boundary for handling missing / invalid models inside Canvas context ---
interface ErrorBoundaryProps {
  fallback: React.ReactNode;
  children: React.ReactNode;
  resetKey?: any;
}

class ErrorBoundary extends React.Component<ErrorBoundaryProps, { hasError: boolean }> {
  constructor(props: ErrorBoundaryProps) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidUpdate(prevProps: ErrorBoundaryProps) {
    if (prevProps.resetKey !== this.props.resetKey) {
      this.setState({ hasError: false });
    }
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback;
    }
    return this.props.children;
  }
}

// --- Canvas Loader & Error Components using Drei's Html wrapper ---
const CanvasLoader: FC = () => {
  return (
    <Html center>
      <div className="flex flex-col items-center justify-center gap-3 whitespace-nowrap bg-[#111827]/90 backdrop-blur-md px-6 py-4 rounded-xl border border-[#1f2937] shadow-2xl">
        <RefreshCw className="w-8 h-8 text-blue-500 animate-spin" />
        <span className="text-sm font-semibold tracking-wide text-neutral-300">Loading 3D asset from DAT...</span>
      </div>
    </Html>
  );
};

interface CanvasErrorProps {
  wcid: number;
  onReset: () => void;
}

const CanvasError: FC<CanvasErrorProps> = ({ wcid, onReset }) => {
  return (
    <Html center>
      <div className="flex flex-col items-center justify-center gap-3 p-6 text-center bg-[#0f172a]/95 border border-red-500/30 rounded-xl w-[320px] shadow-2xl">
        <div className="p-3 bg-red-950/40 border border-red-500/30 text-red-400 rounded-full">
          <AlertTriangle className="w-8 h-8" />
        </div>
        <h3 className="text-lg font-bold text-red-400">Asset Load Failed</h3>
        <p className="text-xs text-neutral-400 leading-relaxed">
          Weenie ID {wcid} could not be loaded. Please ensure this Weenie exists in your database and defines a valid 3D setup model (0x02).
        </p>
        <button
          onClick={onReset}
          className="mt-2 px-4 py-2 bg-[#1f2937] hover:bg-[#374151] border border-[#374151] rounded-lg text-xs font-semibold text-white transition-colors"
        >
          Reset to Default Preset
        </button>
      </div>
    </Html>
  );
};

// --- Curated Preset Lists ---
const PRESET_CREATURES = [
  { wcid: 25749, name: 'Olthoi Harvester' },
  { wcid: 787802041, name: 'Blistered Zombie' },
  { wcid: 787802042, name: 'Shocked Zombie' },
  { wcid: 787802037, name: 'Fire Skeleton Samurai' },
  { wcid: 35146, name: 'Olthoi Slayer' },
  { wcid: 36967, name: 'Tusker Protector' },
  { wcid: 35427, name: 'Drudge Lurker' },
  { wcid: 35134, name: 'Kroktok Lugian' },
];

// --- 3D Model Instance with Custom WebGL Shader ---
interface ModelProps {
  wcid: number;
  paletteId?: number;
  paletteSlot?: number;
  hueShift?: number;
  rotationSpeed: number;
  isRotating: boolean;
  wireframe: boolean;
  activeTexReplaceInfo?: any;
  onCreated: (gl: any) => void;
}

const Model: FC<ModelProps> = ({ wcid, paletteId, paletteSlot = -1, hueShift, activeTexReplaceInfo, rotationSpeed, isRotating, wireframe, onCreated }) => {
  const modelUrl = `/api/visualizer/mesh/${wcid}.gltf?paletteId=${paletteId || 0}&hue=${hueShift || 0}&slot=${paletteSlot}`;

  // useGLTF suspends while parsing binary buffer
  const { scene } = useGLTF(modelUrl);
  const { gl } = useThree();
  const groupRef = useRef<THREE.Group>(null);

  // Expose GL context to parent for screenshots
  useEffect(() => {
    if (gl && onCreated) {
      onCreated(gl);
    }
  }, [gl, onCreated]);

  // Ensure standard materials and apply wireframe
  useEffect(() => {
    scene.traverse((child: any) => {
      if (child.isMesh) {
        if (child.material) {
          if (child.material.map) {
            child.material.map.minFilter = THREE.NearestFilter;
            child.material.map.magFilter = THREE.NearestFilter;
            if (!child.material.userData.originalMap) {
              child.material.userData.originalMap = child.material.map;
            }
          }
          child.material.wireframe = wireframe;
        }
      }
    });
  }, [scene, wireframe]);

  // Texture replacement
  useEffect(() => {
    scene.traverse((child: any) => {
      if (child.isMesh && child.material) {
        const matList = Array.isArray(child.material) ? child.material : [child.material];

        matList.forEach((mat: any) => {
          if (mat && mat.map) {
            if (!mat.userData.originalMap) {
              mat.userData.originalMap = mat.map;
            }

            if (activeTexReplaceInfo) {
              const oldHexUpper = activeTexReplaceInfo.oldTextureId.toString(16).toUpperCase().padStart(8, '0');
              const oldHexLower = activeTexReplaceInfo.oldTextureId.toString(16).toLowerCase().padStart(8, '0');

              const matName = mat.name || '';
              const mapSrc = mat.map.image?.src || mat.userData.originalMap?.image?.src || '';

              const isMatch = activeTexReplaceInfo.isUniversal ||
                              matName.toUpperCase().includes(oldHexUpper) || 
                              matName.toLowerCase().includes(oldHexLower) ||
                              mapSrc.toUpperCase().includes(oldHexUpper) || 
                              mapSrc.toLowerCase().includes(oldHexLower);

              if (isMatch) {
                const newHex = activeTexReplaceInfo.newTextureId.toString(16).toUpperCase().padStart(8, '0');
                const newUrl = `/api/visualizer/texture/${newHex}.png?wcid=${wcid}&paletteId=${paletteId || 0}&hue=${hueShift || 0}&slot=${paletteSlot}`;

                new THREE.TextureLoader().load(newUrl, (tex) => {
                  tex.flipY = false;
                  tex.minFilter = THREE.NearestFilter;
                  tex.magFilter = THREE.NearestFilter;
                  tex.needsUpdate = true;

                  mat.map = tex;
                  mat.needsUpdate = true;
                });
              }
            } else if (mat.userData.originalMap) {
              mat.map = mat.userData.originalMap;
              mat.needsUpdate = true;
            }
          }
        });
      }
    });
  }, [scene, activeTexReplaceInfo, wcid, paletteId, hueShift, paletteSlot]);

  // Center model and scale it
  useEffect(() => {
    if (scene) {
      const box = new THREE.Box3().setFromObject(scene);
      const center = box.getCenter(new THREE.Vector3());
      scene.position.x = -center.x;
      scene.position.y = -center.y;
      scene.position.z = -center.z;
    }
  }, [scene]);

  useFrame((_state, delta) => {
    if (isRotating && groupRef.current) {
      groupRef.current.rotation.y += delta * rotationSpeed;
    }
  });

  return (
    <group ref={groupRef}>
      <primitive object={scene} />
    </group>
  );
};

// --- Main WorldViewer Page ---
const WorldViewer: FC = () => {
  const [wcid, setWcid] = useState<number>(25749); // Default Olthoi Harvester
  const [searchInput, setSearchInput] = useState<string>('25749');
  const [paletteId, setPaletteId] = useState<number>(0); 
  const [hueShift, setHueShift] = useState<number>(0);
  const [speciesPalettes, setSpeciesPalettes] = useState<any[]>([]);
  const [textureReplacements, setTextureReplacements] = useState<any[]>([]);
  const [activeTexReplaceIdx, setActiveTexReplaceIdx] = useState<number>(-1);

  // Smart Palette States
  const [smartPalettes, setSmartPalettes] = useState<any[]>([]);
  const [smartFamily, setSmartFamily] = useState<string>('all');
  const [paletteSlot, setPaletteSlot] = useState<number>(-1);


  const [isRotating, setIsRotating] = useState<boolean>(true);
  const [rotationSpeed, setRotationSpeed] = useState<number>(0.25);
  const [wireframe, setWireframe] = useState<boolean>(false);
  const [showGrid, setShowGrid] = useState<boolean>(true);
  const [lightIntensity, setLightIntensity] = useState<number>(1.2);

  // Bulk Exporter States
  const [isBulkExporting, setIsBulkExporting] = useState<boolean>(false);
  const [bulkProgress, setBulkProgress] = useState<number>(0);
  const [bulkTotal, setBulkTotal] = useState<number>(0);

  // GL Context Ref for screenshots
  const glRef = useRef<any>(null);

  // Universal Texture Swapper States
  const [creatureSurfaces, setCreatureSurfaces] = useState<any[]>([]);
  const [targetSurfaceId, setTargetSurfaceId] = useState<number>(0);
  const [textureLibrary, setTextureLibrary] = useState<any[]>([]);
  const [selectedLibTexId, setSelectedLibTexId] = useState<number>(0);
  const [customTexHex, setCustomTexHex] = useState<string>('');
  const [customPalSetHex, setCustomPalSetHex] = useState<string>('');
  const [similarPalettes, setSimilarPalettes] = useState<any[]>([]);

  useEffect(() => {
    if (paletteId > 0) {
      const palHex = `0x${paletteId.toString(16).toUpperCase()}`;
      fetch(`/api/visualizer/palette/similar/${palHex}`)
        .then(r => r.json())
        .then(data => {
          if (Array.isArray(data)) setSimilarPalettes(data);
        })
        .catch(() => setSimilarPalettes([]));
    }
  }, [paletteId]);

  useEffect(() => {
    fetch(`/api/visualizer/species-palettes/${wcid}`)
      .then(r => r.json())
      .then(data => {
         setSpeciesPalettes(data);
         if (data.length > 0) setPaletteId(data[0].templateId);
         else setPaletteId(0);
         setHueShift(0);
      })
      .catch(e => console.error(e));

    fetch(`/api/visualizer/texture-replacements/${wcid}`)
      .then(r => r.json())
      .then(data => {
         setTextureReplacements(data);
         setActiveTexReplaceIdx(-1);
      })
      .catch(e => console.error(e));

    fetch(`/api/visualizer/surfaces/${wcid}`)
      .then(r => r.json())
      .then(data => {
        setCreatureSurfaces(data);
        if (data.length > 0) setTargetSurfaceId(data[0].textureId);
        else setTargetSurfaceId(0);
      })
      .catch(e => console.error(e));

    fetch(`/api/visualizer/texture-library`)
      .then(r => r.json())
      .then(data => setTextureLibrary(data))
      .catch(e => console.error(e));
  }, [wcid]);

  // Interactive Curation States
  const [curations, setCurations] = useState<Record<string, number>>({});

  // Session Audit Log States
  type LogEntry = {
    id: string;
    timestamp: string;
    type: 'auto' | 'user' | 'screenshot';
    content: string;
  };

  const [sessionLogs, setSessionLogs] = useState<LogEntry[]>([]);
  const [chatInput, setChatInput] = useState<string>('');
  const [isLogOpen, setIsLogOpen] = useState<boolean>(true);
  const logEndRef = useRef<HTMLDivElement>(null);

  const appendLog = (type: 'auto' | 'user' | 'screenshot', content: string) => {
    const timestamp = new Date().toLocaleTimeString('en-US', { hour12: false });
    const newEntry: LogEntry = {
      id: Math.random().toString(36).substring(2, 9),
      timestamp: `[${timestamp}]`,
      type,
      content
    };
    setSessionLogs(prev => [...prev, newEntry]);
  };

  useEffect(() => {
    if (isLogOpen) {
      logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [sessionLogs, isLogOpen]);

  const handleResetToDefault = () => {
    setPaletteId(0);
    setHueShift(0);
    setPaletteSlot(-1);
    setActiveTexReplaceIdx(-1);
    if (creatureSurfaces.length > 0) setTargetSurfaceId(creatureSurfaces[0].textureId);
    else setTargetSurfaceId(0);

    const cName = PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`;
    appendLog('auto', `🔄 Reset visual overrides to native DAT defaults for ${cName}`);
  };

  const handleAddUserComment = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!chatInput.trim()) return;

    appendLog('user', chatInput.trim());
    setChatInput('');
  };

  const handleCaptureScreenshotAndLog = async () => {
    if (!glRef.current) {
      alert("3D canvas not ready for screenshot.");
      return;
    }

    try {
      const dataUrl = glRef.current.domElement.toDataURL("image/png");
      const cName = PRESET_CREATURES.find(c => c.wcid === wcid)?.name.replace(/[^a-zA-Z0-9]/g, '_') || `WCID_${wcid}`;
      const filename = `snapshot_${cName}_${Date.now()}.png`;

      const response = await fetch('/api/visualizer/save-screenshot', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ dataUrl, filename })
      });

      if (response.ok) {
        const imageUrl = `/screenshots/${filename}`;
        appendLog('screenshot', `📸 Captured Screenshot:\n![Snapshot](${imageUrl})`);
      } else {
        appendLog('auto', `⚠️ Failed to save screenshot to server.`);
      }
    } catch (err) {
      console.error("Screenshot capture failed: ", err);
    }
  };

  const handleCopyLogsForAI = async () => {
    if (sessionLogs.length === 0) {
      alert("No logs to copy yet.");
      return;
    }

    const cName = PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`;
    const header = `# 🎨 3D Showroom Session Audit & Comments\n**Creature**: ${cName} (WCID ${wcid})\n**Date**: ${new Date().toLocaleString()}\n\n---\n\n`;
    
    const body = sessionLogs.map(log => {
      if (log.type === 'user') {
        return `### 💬 User Comment ${log.timestamp}\n${log.content}`;
      } else if (log.type === 'screenshot') {
        return `### ${log.content}`;
      }
      return `${log.timestamp} ${log.content}`;
    }).join('\n\n');

    const fullMarkdown = header + body;
    await navigator.clipboard.writeText(fullMarkdown);
    alert("Copied full session transcript (with comments & screenshot links) to clipboard! You can paste it directly into our chat.");
  };

  useEffect(() => {
    fetch(`/api/visualizer/curation/${wcid}`)
      .then(r => r.json())
      .then((data: any[]) => {
        const map: Record<string, number> = {};
        if (Array.isArray(data)) {
          data.forEach(item => {
            const key = `${item.creatureWcid}_${item.textureId}_${item.paletteId}`;
            map[key] = item.rating;
          });
        }
        setCurations(map);
      })
      .catch(e => console.error(e));
  }, [wcid]);

  useEffect(() => {
    fetch(`/api/visualizer/curated-pool/${wcid}?family=${smartFamily}`)
      .then(r => r.json())
      .then(data => {
         setSmartPalettes(data);
      })
      .catch(e => console.error(e));
  }, [wcid, smartFamily]);

  const activeCurrentTexId = activeTexReplaceIdx >= 0 ? textureReplacements[activeTexReplaceIdx]?.newTextureId : targetSurfaceId;
  const currentCurationKey = `${wcid}_${activeCurrentTexId || 0}_${paletteId || 0}`;
  const currentCurationRating = curations[currentCurationKey] || 0;

  const cyclePalette = (direction: 1 | -1) => {
    if (!smartPalettes || smartPalettes.length === 0) return;
    const currIdx = smartPalettes.findIndex(p => p.paletteId === paletteId);
    let nextIdx = 0;
    if (currIdx >= 0) {
      nextIdx = (currIdx + direction + smartPalettes.length) % smartPalettes.length;
    } else {
      nextIdx = direction === 1 ? 0 : smartPalettes.length - 1;
    }
    setPaletteId(smartPalettes[nextIdx].paletteId);
  };

  const handleCurationSubmit = (rating: number) => {
    const activeTexId = activeTexReplaceIdx >= 0 ? textureReplacements[activeTexReplaceIdx]?.newTextureId : targetSurfaceId;
    const activePalId = paletteId || 0;

    const payload = {
      creatureWcid: wcid,
      creatureName: PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`,
      textureId: activeTexId || 0,
      paletteId: activePalId,
      rating: rating
    };

    fetch('/api/visualizer/curation', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
      .then(r => r.json())
      .then(() => {
        const key = `${wcid}_${payload.textureId}_${payload.paletteId}`;
        setCurations(prev => ({ ...prev, [key]: rating }));
        const label = rating === 1 ? '👍 APPROVED' : '👎 BLACKLISTED';
        appendLog('auto', `${label} combo (Texture 0x${payload.textureId.toString(16).toUpperCase().padStart(8, '0')}, Palette 0x${payload.paletteId.toString(16).toUpperCase().padStart(8, '0')})`);
      })
      .catch(e => console.error("Failed to submit curation: ", e));
  };

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (['INPUT', 'SELECT', 'TEXTAREA'].includes((e.target as HTMLElement)?.tagName)) return;

      if (e.key === 'ArrowRight' || e.key === 'd' || e.key === 'D') {
        e.preventDefault();
        cyclePalette(1);
      } else if (e.key === 'ArrowLeft' || e.key === 'q' || e.key === 'Q') {
        e.preventDefault();
        cyclePalette(-1);
      } else if (e.key === 'a' || e.key === 'A') {
        handleCurationSubmit(1);
        cyclePalette(1);
      } else if (e.key === 'x' || e.key === 'X') {
        handleCurationSubmit(-1);
        cyclePalette(1);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [wcid, activeTexReplaceIdx, targetSurfaceId, paletteId, textureReplacements, smartPalettes]);

  const randomizePalette = () => {
    if (speciesPalettes.length > 0 && Math.random() > 0.5) {
      const randomPal = speciesPalettes[Math.floor(Math.random() * speciesPalettes.length)];
      setPaletteId(randomPal.templateId);
      setHueShift(0);
    } else {
      setHueShift(Math.floor(Math.random() * 360));
    }
  };

  const applyUniversalTextureSwap = () => {
    if (!targetSurfaceId) return;

    let newTexId = selectedLibTexId;
    if (customTexHex) {
      const cleanHex = customTexHex.startsWith('0x') ? customTexHex.substring(2) : customTexHex;
      const parsed = parseInt(cleanHex, 16);
      if (!isNaN(parsed) && parsed > 0) {
        newTexId = parsed;
      }
    }

    if (!newTexId) return;

    const newSwap = {
      name: `Dynamic Surface Swap (0x${targetSurfaceId.toString(16).toUpperCase()} -> 0x${newTexId.toString(16).toUpperCase()})`,
      oldTextureId: targetSurfaceId,
      newTextureId: newTexId,
      isUniversal: true
    };

    const nextList = [...textureReplacements, newSwap];
    setTextureReplacements(nextList);
    setActiveTexReplaceIdx(nextList.length - 1);
    appendLog('auto', `⚡ Live Swapped Surface 0x${targetSurfaceId.toString(16).toUpperCase()} -> Texture 0x${newTexId.toString(16).toUpperCase()}`);
  };

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const id = parseInt(searchInput, 10);
    if (!isNaN(id) && id > 0) {
      setWcid(id);
    }
  };

  const selectPreset = (presetWcid: number) => {
    setWcid(presetWcid);
    setSearchInput(presetWcid.toString());
  };

  const startBulkExport = async () => {
    if (isBulkExporting) return;
    
    const confirmStart = window.confirm(
      "This will automatically cycle through all creature presets and palettes to export 2D PNG images. It takes about 1.5 seconds per image. Start?"
    );
    if (!confirmStart) return;

    setIsBulkExporting(true);
    setIsRotating(false); // Stop rotation to get a consistent front angle

    // Generate combinations
    const combinations: { wcid: number; name: string; paletteId: number; }[] = [];
    for (const creature of PRESET_CREATURES) {
      combinations.push({
        wcid: creature.wcid,
        name: creature.name.replace(/\s+/g, ''),
        paletteId: 0
      });
    }

    setBulkTotal(combinations.length);
    setBulkProgress(0);

    for (let i = 0; i < combinations.length; i++) {
      const item = combinations[i];
      setWcid(item.wcid);
      setSearchInput(item.wcid.toString());
      setPaletteId(item.paletteId);

      // Wait for assets to download and render
      await new Promise((resolve) => setTimeout(resolve, 1500));

      if (glRef.current) {
        try {
          const dataUrl = glRef.current.domElement.toDataURL("image/png");
          const filename = `${item.wcid}_${item.name}_${item.paletteId}.png`;

          await fetch('/api/visualizer/save-screenshot', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ dataUrl, filename })
          });
        } catch (err) {
          console.error("Screenshot export failed: ", err);
        }
      }

      setBulkProgress(i + 1);
    }

    setIsBulkExporting(false);
    setIsRotating(true);
    alert("Bulk export complete! All screenshots saved in your server's wwwroot/screenshots directory.");
  };

  return (
    <div className="w-full h-full flex flex-col md:flex-row bg-[#0b0f19] text-[#e2e8f0] font-sans">
      
      {/* Sidebar Controls */}
      <div className="w-full md:w-80 flex-shrink-0 bg-[#111827] border-b md:border-b-0 md:border-r border-[#1f2937] p-5 flex flex-col gap-6 overflow-y-auto">
        <div className="flex items-center gap-3">
          <div className="p-2 bg-blue-600/20 text-blue-400 rounded-lg">
            <Globe className="w-6 h-6 animate-pulse" />
          </div>
          <div>
            <h1 className="text-xl font-bold tracking-tight">3D Showroom</h1>
            <p className="text-xs text-neutral-400">Portal DAT asset renderer</p>
          </div>
        </div>

        {/* Reset to Default Control */}
        <button
          onClick={handleResetToDefault}
          className="w-full flex items-center justify-center gap-2 py-2 bg-neutral-800 hover:bg-neutral-700 text-amber-400 hover:text-amber-300 font-semibold rounded-lg text-xs transition-colors border border-[#374151]"
          title="Reset model overrides to native DAT defaults"
        >
          <RotateCcw className="w-3.5 h-3.5" /> 🔄 Reset to Default
        </button>

        <hr className="border-[#1f2937]" />

        {/* Model Search */}
        <form onSubmit={handleSearchSubmit} className="flex flex-col gap-2">
          <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400">Search Weenie Class ID</label>
          <div className="flex gap-2">
            <div className="relative flex-grow">
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Enter WCID..."
                className="w-full pl-9 pr-3 py-2 bg-[#1f2937] border border-[#374151] rounded-lg text-sm focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              />
              <Search className="w-4 h-4 text-neutral-400 absolute left-3 top-3" />
            </div>
            <button
              type="submit"
              className="px-4 py-2 bg-blue-600 hover:bg-blue-500 font-semibold rounded-lg text-sm transition-colors"
            >
              Load
            </button>
          </div>
        </form>

        {/* Curated Presets */}
        <div className="flex flex-col gap-2">
          <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400">Creature Presets</label>
          <div className="flex flex-col gap-1.5">
            {PRESET_CREATURES.map((creature) => (
              <button
                key={creature.wcid}
                onClick={() => selectPreset(creature.wcid)}
                className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-left text-sm transition-all ${
                  wcid === creature.wcid 
                    ? 'bg-blue-600/20 border border-blue-500/50 text-blue-300' 
                    : 'bg-[#1f2937]/50 hover:bg-[#1f2937] border border-transparent text-neutral-300'
                }`}
              >
                <span>{creature.name}</span>
                <ChevronRight className="w-4 h-4 text-neutral-500" />
              </button>
            ))}
          </div>
        </div>

        {/* Palette Selector */}
        <div className="flex flex-col gap-2">
          <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5">
            <PaletteIcon className="w-4 h-4 text-neutral-400" />
            Species Variant Palette
          </label>
          <select
            value={(paletteId & 0xFF000000) === 0x04000000 ? 0 : paletteId}
            onChange={(e) => setPaletteId(parseInt(e.target.value))}
            className="w-full px-3 py-2 bg-[#1f2937] border border-[#374151] rounded-lg text-sm text-white focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
          >
            <option value={0}>Default Variant</option>
            {speciesPalettes.map(pal => (
              <option key={pal.templateId} value={pal.templateId}>
                {pal.name} (0x{pal.paletteId.toString(16).toUpperCase()})
              </option>
            ))}
          </select>

          {/* Active Species Variant Metadata & Swatches Card */}
          {(() => {
            const activeVariant = speciesPalettes.find(p => p.templateId === paletteId);
            if (!activeVariant) return null;
            return (
              <div className="mt-2 p-2.5 bg-[#111827] rounded-lg border border-[#374151] flex flex-col gap-2">
                <div className="flex items-center justify-between text-xs">
                  <span className="font-bold text-amber-400">{activeVariant.name} Metadata</span>
                  <span className="font-mono text-[10px] text-neutral-400">Template #{activeVariant.templateId}</span>
                </div>

                <div className="grid grid-cols-2 gap-1.5 text-[11px] font-mono text-neutral-300 bg-[#1f2937]/50 p-2 rounded border border-[#374151]/40">
                  <div>
                    <span className="text-neutral-500 block text-[9px] uppercase">PaletteSet</span>
                    <span className="text-blue-300 font-bold">{activeVariant.paletteSetHex || '0x0F000202'}</span>
                  </div>
                  <div>
                    <span className="text-neutral-500 block text-[9px] uppercase">Resolved Palette</span>
                    <span className="text-green-300 font-bold">{activeVariant.paletteHex || '0x04001163'}</span>
                  </div>
                </div>

                {/* Live Swatch Bar */}
                {activeVariant.swatches && activeVariant.swatches.length > 0 && (
                  <div className="flex flex-col gap-1">
                    <span className="text-[10px] uppercase font-semibold text-neutral-400">Gradient Swatches (8-Point)</span>
                    <div className="flex gap-1 h-5 rounded overflow-hidden border border-[#374151]">
                      {activeVariant.swatches.map((hex: string, idx: number) => (
                        <div 
                          key={idx} 
                          className="flex-1 h-full cursor-pointer hover:opacity-80 transition-opacity" 
                          style={{ backgroundColor: hex }}
                          title={`Swatch #${idx + 1}: ${hex}`}
                        />
                      ))}
                    </div>
                  </div>
                )}

                <div className="text-[10px] text-neutral-400 font-mono italic">
                  Range: {activeVariant.ranges || 'Offset 0 - 2048 (Full Mesh)'}
                </div>

                {/* Copy SQL / Command Button */}
                <button
                  onClick={() => {
                    const sql = `UPDATE \`weenie_properties_int\` SET \`value\` = ${activeVariant.templateId} WHERE \`weenie_class_Id\` = ${wcid} AND \`type\` = 3;`;
                    const cmd = `@set-prop int 3 ${activeVariant.templateId}`;
                    navigator.clipboard.writeText(`${sql}\n-- In-Game Command:\n${cmd}`);
                    alert(`Copied in-game SQL and command to clipboard!\n\n${cmd}`);
                  }}
                  className="w-full py-1 px-2 bg-blue-600/20 hover:bg-blue-600/40 text-blue-300 border border-blue-500/30 rounded text-[11px] font-semibold flex items-center justify-center gap-1 transition-colors"
                >
                  📋 Copy In-Game SQL & Command
                </button>
              </div>
            );
          })()}

          {/* PERMANENT PaletteSet / Palette Direct ID Override Toolbar */}
          <div className="mt-2 p-2.5 bg-[#111827] rounded-lg border border-[#374151] flex flex-col gap-1.5 shadow-sm">
            <span className="text-[10px] font-semibold text-amber-300 uppercase tracking-wide flex items-center gap-1">
              ⚡ Direct PaletteSet (0x0F...) or Palette (0x04...) ID Override
            </span>
            <div className="flex gap-1.5">
              <input
                type="text"
                placeholder="e.g. 0x0F0001FF or 0x04001091"
                value={customPalSetHex}
                onChange={(e) => setCustomPalSetHex(e.target.value)}
                className="flex-grow px-2 py-1 bg-[#1f2937] border border-[#374151] rounded text-[11px] text-white font-mono placeholder-neutral-500 focus:outline-none focus:border-amber-500"
              />
              <button
                type="button"
                onClick={() => {
                  if (!customPalSetHex.trim()) return;
                  const clean = customPalSetHex.trim().toLowerCase().replace('0x', '');
                  const parsed = parseInt(clean, 16);
                  if (!isNaN(parsed) && parsed > 0) {
                    setPaletteId(parsed);
                    appendLog('auto', `🎨 Overwrote PaletteSet / Palette ID to 0x${parsed.toString(16).toUpperCase()}`);
                  } else {
                    alert("Invalid Hex ID. Use format 0x0F0001FF or 0x04001165");
                  }
                }}
                className="px-3 py-1 bg-amber-600 hover:bg-amber-500 text-white font-bold rounded text-[11px] transition-colors shadow"
              >
                Apply
              </button>
            </div>
          </div>
          
          <div className="flex flex-col gap-1 mt-2">
            <span className="flex items-center gap-1.5 text-sm text-neutral-300">
              Hue Shift ({hueShift}°)
            </span>
            <input
              type="range"
              min="0"
              max="360"
              step="1"
              value={hueShift}
              onChange={(e) => setHueShift(parseInt(e.target.value))}
              className="w-full h-1.5 bg-gradient-to-r from-red-500 via-green-500 to-blue-500 rounded-lg appearance-none cursor-pointer"
            />
          </div>

          <button
            onClick={randomizePalette}
            className="mt-2 w-full flex items-center justify-center gap-2 py-2 bg-[#1f2937] hover:bg-[#374151] text-white font-semibold rounded-lg text-sm transition-colors border border-[#374151]"
          >
            🎲 Randomize Palette
          </button>
        </div>

        {/* Universal Programmatic Surface Texture Swapper (On-The-Fly) */}
        <div className="flex flex-col gap-2 bg-[#1f2937]/30 p-3 rounded-lg border border-[#374151]/60">
          <label className="text-xs font-semibold uppercase tracking-wider text-blue-400 flex items-center gap-1.5">
            <Sliders className="w-4 h-4 text-blue-400" />
            Universal Surface Texture Swapper (On-The-Fly)
          </label>
          <p className="text-[11px] text-neutral-400 leading-normal">
            Swap surface textures live on the 3D model programmatically without needing pre-written JSON files.
          </p>

          <div className="flex flex-col gap-1 mt-1">
            <span className="text-[11px] font-semibold text-neutral-300">1. Target Active Surface</span>
            <select
              value={targetSurfaceId}
              onChange={(e) => setTargetSurfaceId(parseInt(e.target.value))}
              className="w-full px-2.5 py-1.5 bg-[#111827] border border-[#374151] rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
            >
              <option value={0}>Select Surface to Replace...</option>
              {creatureSurfaces.map(surf => (
                <option key={surf.textureId} value={surf.textureId}>
                  {surf.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex flex-col gap-1 mt-1">
            <span className="text-[11px] font-semibold text-neutral-300">2. Preset DAT Texture</span>
            <select
              value={selectedLibTexId}
              onChange={(e) => {
                const val = parseInt(e.target.value);
                setSelectedLibTexId(val);
                if (val > 0) setCustomTexHex('');
              }}
              className="w-full px-2.5 py-1.5 bg-[#111827] border border-[#374151] rounded-lg text-xs text-white focus:outline-none focus:border-blue-500"
            >
              <option value={0}>Select from Texture Library...</option>
              {textureLibrary.map(item => (
                <option key={item.textureId} value={item.textureId}>
                  [{item.category}] {item.name} ({item.hexId})
                </option>
              ))}
            </select>
          </div>

          <div className="flex flex-col gap-1 mt-1">
            <span className="text-[11px] font-semibold text-neutral-300">OR Enter Custom DAT Texture ID (Hex)</span>
            <input
              type="text"
              placeholder="e.g. 0x06004067"
              value={customTexHex}
              onChange={(e) => {
                setCustomTexHex(e.target.value);
                if (e.target.value) setSelectedLibTexId(0);
              }}
              className="w-full px-2.5 py-1.5 bg-[#111827] border border-[#374151] rounded-lg text-xs text-white placeholder-neutral-500 focus:outline-none focus:border-blue-500 font-mono"
            />
          </div>

          <button
            onClick={applyUniversalTextureSwap}
            disabled={!targetSurfaceId || (!selectedLibTexId && !customTexHex)}
            className="mt-2 w-full flex items-center justify-center gap-1.5 py-2 bg-blue-600 hover:bg-blue-500 disabled:bg-neutral-800 disabled:text-neutral-500 text-white font-semibold rounded-lg text-xs transition-colors shadow-md"
          >
            ⚡ Apply Texture Swap Live
          </button>
        </div>

        {/* Smart Palette Inspector */}
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5">
              <Layers className="w-4 h-4 text-neutral-400" />
              Material & Palette Inspector
            </label>
          </div>

          {/* Target Body Part / Subpalette Slot Selector */}
          <div className="flex flex-col gap-1 my-1 bg-[#1f2937]/40 p-2.5 rounded-lg border border-[#374151]/50">
            <label className="text-[11px] font-semibold text-blue-400 flex items-center gap-1">
              🎯 Target Body Part / Subpalette Slot
            </label>
            <select
              value={paletteSlot}
              onChange={(e) => setPaletteSlot(parseInt(e.target.value))}
              className="w-full bg-[#111827] text-white border border-[#374151] rounded-lg px-2.5 py-1.5 text-xs font-semibold focus:outline-none focus:border-blue-500 cursor-pointer"
            >
              <option value={-1}>🌟 All Body Parts (Entire Model)</option>
              <option value={1}>🐺 Primary Body / Fur (Slot 1)</option>
              <option value={2}>🛡️ Armor & Clothing Trim (Slot 2)</option>
              <option value={3}>🦷 Tusks, Claws & Accents (Slot 3)</option>
              <option value={4}>✨ Detail Highlights (Slot 4)</option>
            </select>
          </div>

          <div className="flex gap-1 overflow-x-auto pb-2 scrollbar-thin scrollbar-thumb-neutral-600 scrollbar-track-transparent">
            {['All', 'Chitin', 'Fur/Hide', 'Metallic', 'Elemental'].map(family => (
              <button
                key={family}
                onClick={() => setSmartFamily(family === 'All' ? 'all' : family)}
                className={`px-3 py-1 text-[11px] font-semibold rounded-full whitespace-nowrap transition-colors ${
                  (smartFamily === 'all' && family === 'All') || smartFamily === family
                    ? 'bg-blue-600 text-white' 
                    : 'bg-[#1f2937] text-neutral-400 hover:text-neutral-200'
                }`}
              >
                {family}
              </button>
            ))}
          </div>

          <div className="grid grid-cols-2 gap-2 max-h-48 overflow-y-auto scrollbar-thin scrollbar-thumb-neutral-600 pr-1">
            {smartPalettes.map(pal => (
              <button
                key={pal.paletteId}
                onClick={() => setPaletteId(pal.paletteId)}
                className={`flex flex-col gap-1 p-2 rounded border text-left transition-all ${
                  paletteId === pal.paletteId
                    ? 'bg-blue-600/20 border-blue-500'
                    : 'bg-[#1f2937]/50 border-[#374151] hover:border-neutral-500'
                }`}
              >
                <div className="flex justify-between items-center text-xs">
                  <span className="font-mono text-neutral-300">{pal.hexId}</span>
                </div>
                <div className="flex w-full h-3 rounded overflow-hidden">
                  {pal.swatches.map((hex: string, i: number) => (
                    <div key={i} className="flex-1 h-full" style={{ backgroundColor: hex }} />
                  ))}
                </div>
              </button>
            ))}
            {smartPalettes.length === 0 && (
              <div className="col-span-2 text-center text-xs text-neutral-500 py-4">
                No palettes found for this material family.
              </div>
            )}
          </div>

          {/* Similar Palettes Recommendation Shelf (CIELAB Delta-E) */}
          {similarPalettes.length > 0 && (
            <div className="flex flex-col gap-1.5 mt-2 pt-2 border-t border-[#374151]/50">
              <span className="text-[11px] font-semibold text-purple-400 uppercase tracking-wide flex items-center gap-1">
                ✨ Palettes with Similar Progression (CIELAB Delta-E)
              </span>
              <div className="grid grid-cols-2 gap-1.5">
                {similarPalettes.map(sim => (
                  <button
                    key={sim.paletteId}
                    onClick={() => {
                      setPaletteId(sim.paletteId);
                      appendLog('auto', `✨ Selected Similar Palette 0x${sim.paletteId.toString(16).toUpperCase()}`);
                    }}
                    className="flex flex-col gap-1 p-1.5 rounded bg-[#1f2937]/70 hover:bg-[#374151] border border-purple-500/30 hover:border-purple-400 text-left transition-all"
                  >
                    <div className="flex justify-between items-center text-[10px]">
                      <span className="font-mono text-purple-200">{sim.hexId}</span>
                      <span className="text-[9px] text-neutral-400">{sim.family}</span>
                    </div>
                    <div className="flex w-full h-2.5 rounded overflow-hidden">
                      {sim.swatches.map((hex: string, i: number) => (
                        <div key={i} className="flex-1 h-full" style={{ backgroundColor: hex }} />
                      ))}
                    </div>
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {/* Texture Replacement Selector */}
        {textureReplacements.length > 0 && (
          <div className="flex flex-col gap-2">
            <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5">
              <Layers className="w-4 h-4 text-neutral-400" />
              Texture Replacement
            </label>
            <select
              value={activeTexReplaceIdx}
              onChange={(e) => setActiveTexReplaceIdx(parseInt(e.target.value))}
              className="w-full px-3 py-2 bg-[#1f2937] border border-[#374151] rounded-lg text-sm text-white focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
            >
              <option value={-1}>Original Textures</option>
              {textureReplacements.map((tr, idx) => (
                <option key={idx} value={idx}>
                  {tr.name} (0x{tr.newTextureId.toString(16).toUpperCase()})
                </option>
              ))}
            </select>
          </div>
        )}

        <hr className="border-[#1f2937]" />

        {/* Display Settings */}
        <div className="flex flex-col gap-3">
          <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400">Settings</label>
          
          <div className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-1.5 text-neutral-300">
              <RotateCw className="w-4 h-4 text-neutral-400" /> Auto-Rotate
            </span>
            <button
              onClick={() => setIsRotating(!isRotating)}
              className={`p-1.5 rounded-lg border transition-all ${
                isRotating 
                  ? 'bg-blue-600/10 border-blue-500/50 text-blue-400' 
                  : 'bg-transparent border-[#374151] text-neutral-400'
              }`}
            >
              {isRotating ? <Play className="w-4 h-4" /> : <Pause className="w-4 h-4" />}
            </button>
          </div>

          {isRotating && (
            <div className="flex flex-col gap-1">
              <span className="text-[11px] text-neutral-400">Rotation Speed</span>
              <input
                type="range"
                min="0.05"
                max="1.0"
                step="0.05"
                value={rotationSpeed}
                onChange={(e) => setRotationSpeed(parseFloat(e.target.value))}
                className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-blue-500"
              />
            </div>
          )}

          <div className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-1.5 text-neutral-300">
              <Grid className="w-4 h-4 text-neutral-400" /> Show Grid
            </span>
            <input
              type="checkbox"
              checked={showGrid}
              onChange={() => setShowGrid(!showGrid)}
              className="w-4 h-4 rounded border-[#374151] bg-[#1f2937] text-blue-600 focus:ring-blue-500"
            />
          </div>

          <div className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-1.5 text-neutral-300">
              <Eye className="w-4 h-4 text-neutral-400" /> Wireframe Mode
            </span>
            <input
              type="checkbox"
              checked={wireframe}
              onChange={() => setWireframe(!wireframe)}
              className="w-4 h-4 rounded border-[#374151] bg-[#1f2937] text-blue-600 focus:ring-blue-500"
            />
          </div>

          <div className="flex flex-col gap-1">
            <span className="flex items-center gap-1.5 text-sm text-neutral-300">
              <Sun className="w-4 h-4 text-neutral-400" /> Brightness ({lightIntensity.toFixed(1)}x)
            </span>
            <input
              type="range"
              min="0.5"
              max="2.5"
              step="0.1"
              value={lightIntensity}
              onChange={(e) => setLightIntensity(parseFloat(e.target.value))}
              className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-blue-500"
            />
          </div>
        </div>

        <hr className="border-[#1f2937]" />

        {/* Bulk Screenshot Exporter */}
        <div className="flex flex-col gap-3">
          <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5">
            <Camera className="w-4 h-4 text-neutral-400" />
            Bulk Asset Rendering
          </label>
          <p className="text-xs text-neutral-400 leading-relaxed">
            Automatically render all combinations of creature presets and palettes, saving them as high-quality PNGs in your server's <code className="text-blue-400 bg-neutral-900 px-1 py-0.5 rounded font-mono">wwwroot/screenshots</code> folder.
          </p>
          
          {isBulkExporting ? (
            <div className="flex flex-col gap-2 bg-[#1f2937]/30 border border-[#374151] rounded-lg p-3">
              <div className="flex items-center justify-between text-xs">
                <span className="text-neutral-300 font-semibold flex items-center gap-1.5 animate-pulse">
                  <RefreshCw className="w-3.5 h-3.5 animate-spin text-blue-400" />
                  Generating {bulkProgress} / {bulkTotal}...
                </span>
                <span className="text-neutral-400">{Math.round((bulkProgress / bulkTotal) * 100)}%</span>
              </div>
              <div className="w-full bg-[#111827] rounded-full h-1.5 overflow-hidden">
                <div 
                  className="bg-blue-500 h-1.5 rounded-full transition-all duration-300"
                  style={{ width: `${(bulkProgress / bulkTotal) * 100}%` }}
                />
              </div>
            </div>
          ) : (
            <button
              onClick={startBulkExport}
              className="w-full flex items-center justify-center gap-2 py-2 bg-blue-600 hover:bg-blue-500 text-white font-semibold rounded-lg text-sm transition-colors shadow-lg shadow-blue-600/20"
            >
              <Download className="w-4 h-4" />
              Bulk Export 2D Images
            </button>
          )}
        </div>
      </div>

      {/* Main Canvas Area */}
      <div className="flex-grow relative h-[500px] md:h-full min-h-[300px] bg-[#090d16] flex items-center justify-center">
        
        {/* 3D Canvas */}
        <div className="w-full h-full">
          <Canvas
            camera={{ position: [0, 0, 4.5], fov: 45 }}
            gl={{ preserveDrawingBuffer: true, antialias: true }}
          >
            <color attach="background" args={['#090d16']} />
            <ambientLight intensity={lightIntensity * 0.4} />
            <directionalLight position={[10, 10, 5]} intensity={lightIntensity * 0.8} castShadow />
            <directionalLight position={[-10, 5, -5]} intensity={lightIntensity * 0.3} />
            <pointLight position={[0, -5, 5]} intensity={lightIntensity * 0.4} />

            <ErrorBoundary
              resetKey={wcid}
              fallback={<CanvasError wcid={wcid} onReset={() => selectPreset(25749)} />}
            >
              <Suspense fallback={<CanvasLoader />}>
                <Model
                  wcid={wcid}
                  paletteId={paletteId}
                  paletteSlot={paletteSlot}
                  hueShift={hueShift}
                  activeTexReplaceInfo={activeTexReplaceIdx >= 0 ? textureReplacements[activeTexReplaceIdx] : null}
                  rotationSpeed={rotationSpeed}
                  isRotating={isRotating}
                  wireframe={wireframe}
                  onCreated={(gl) => { glRef.current = gl; }}
                />
              </Suspense>
            </ErrorBoundary>

            {showGrid && (
              <gridHelper args={[15, 15, '#1e293b', '#0f172a']} position={[0, -1.2, 0]} />
            )}
            <OrbitControls 
              enableDamping 
              dampingFactor={0.05} 
              minDistance={1.5} 
              maxDistance={12} 
              target={[0, 0, 0]}
            />
          </Canvas>
        </div>

        {/* Interactive Curation Toolbar (Approval / Blacklist) */}
        <div className="absolute top-4 right-4 bg-[#111827]/90 backdrop-blur-md px-4 py-3 rounded-xl border border-[#374151] shadow-2xl flex items-center gap-3 select-none z-10">
          <div className="flex flex-col">
            <span className="text-[10px] uppercase font-bold text-blue-400 tracking-wider">Quality Control</span>
            <div className="flex items-center gap-1.5 text-xs font-semibold text-white">
              {currentCurationRating === 1 && <span className="text-green-400 flex items-center gap-1"><CheckCircle2 className="w-4 h-4" /> Approved</span>}
              {currentCurationRating === -1 && <span className="text-red-400 flex items-center gap-1"><XCircle className="w-4 h-4" /> Blacklisted</span>}
              {currentCurationRating === 0 && <span className="text-neutral-400 flex items-center gap-1"><AlertTriangle className="w-4 h-4 text-amber-400" /> Unrated</span>}
            </div>
          </div>

          <div className="h-6 w-[1px] bg-[#374151]" />

          <div className="flex items-center gap-2">
            <button
              onClick={() => handleCurationSubmit(1)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all shadow-md ${
                currentCurationRating === 1
                  ? 'bg-green-600 text-white ring-2 ring-green-400'
                  : 'bg-green-600/20 text-green-400 hover:bg-green-600 hover:text-white border border-green-500/40'
              }`}
              title="Approve Combination (Shortcut: A)"
            >
              <ThumbsUp className="w-3.5 h-3.5" /> Approve (A)
            </button>

            <button
              onClick={() => handleCurationSubmit(-1)}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition-all shadow-md ${
                currentCurationRating === -1
                  ? 'bg-red-600 text-white ring-2 ring-red-400'
                  : 'bg-red-600/20 text-red-400 hover:bg-red-600 hover:text-white border border-red-500/40'
              }`}
              title="Blacklist Combination (Shortcut: X)"
            >
              <ThumbsDown className="w-3.5 h-3.5" /> Blacklist (X)
            </button>
          </div>
        </div>

        {/* Showroom Session Audit & Chat Box */}
        <div className="absolute bottom-4 right-4 w-96 max-w-[90vw] bg-[#111827]/95 backdrop-blur-md rounded-xl border border-[#374151] shadow-2xl flex flex-col z-20 overflow-hidden">
          {/* Header */}
          <div 
            onClick={() => setIsLogOpen(!isLogOpen)}
            className="px-3.5 py-2.5 bg-[#1f2937]/80 hover:bg-[#1f2937] border-b border-[#374151] flex items-center justify-between cursor-pointer select-none"
          >
            <div className="flex items-center gap-2">
              <MessageSquare className="w-4 h-4 text-blue-400" />
              <span className="text-xs font-bold text-white tracking-wide">Session Audit & Comments</span>
              <span className="text-[10px] bg-blue-600/30 text-blue-300 font-mono px-1.5 py-0.5 rounded-full">
                {sessionLogs.length}
              </span>
            </div>

            <div className="flex items-center gap-1.5">
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  handleCopyLogsForAI();
                }}
                className="p-1 hover:bg-blue-600/30 text-neutral-300 hover:text-white rounded transition-colors"
                title="Copy Full Session Log for AI"
              >
                <Copy className="w-3.5 h-3.5" />
              </button>
              {isLogOpen ? <ChevronDown className="w-4 h-4 text-neutral-400" /> : <ChevronUp className="w-4 h-4 text-neutral-400" />}
            </div>
          </div>

          {/* Collapsible Content */}
          {isLogOpen && (
            <div className="flex flex-col">
              {/* Log Messages Viewport */}
              <div className="h-52 p-3 overflow-y-auto flex flex-col gap-2 font-sans text-xs scrollbar-thin scrollbar-thumb-neutral-700">
                {sessionLogs.length === 0 ? (
                  <div className="text-center text-neutral-500 text-[11px] my-auto italic">
                    No actions logged yet. Interact with the 3D model, swap textures, or add comments below!
                  </div>
                ) : (
                  sessionLogs.map(log => (
                    <div 
                      key={log.id} 
                      className={`p-2 rounded-lg text-xs leading-relaxed border ${
                        log.type === 'user' 
                          ? 'bg-blue-900/30 border-blue-500/40 text-blue-100' 
                          : log.type === 'screenshot'
                          ? 'bg-purple-900/30 border-purple-500/40 text-purple-100'
                          : 'bg-[#1f2937]/50 border-[#374151]/80 text-neutral-300'
                      }`}
                    >
                      <div className="flex items-center justify-between text-[10px] opacity-70 mb-0.5 font-mono">
                        <span>{log.type === 'user' ? '💬 Comment' : log.type === 'screenshot' ? '📸 Snapshot' : '⚡ System Event'}</span>
                        <span>{log.timestamp}</span>
                      </div>
                      <div className="whitespace-pre-wrap break-words">
                        {log.content}
                      </div>
                    </div>
                  ))
                )}
                <div ref={logEndRef} />
              </div>

              {/* Chat Input & Tools Bar */}
              <form onSubmit={handleAddUserComment} className="p-2 bg-[#1f2937]/50 border-t border-[#374151] flex flex-col gap-2">
                <div className="flex gap-1.5">
                  <input
                    type="text"
                    value={chatInput}
                    onChange={(e) => setChatInput(e.target.value)}
                    placeholder="Type a comment/note on this combo..."
                    className="flex-grow px-2.5 py-1.5 bg-[#0b0f19] border border-[#374151] rounded-lg text-xs text-white placeholder-neutral-500 focus:outline-none focus:border-blue-500"
                  />
                  <button
                    type="submit"
                    className="px-3 py-1.5 bg-blue-600 hover:bg-blue-500 text-white font-semibold rounded-lg text-xs transition-colors flex items-center gap-1 shadow"
                  >
                    <Send className="w-3 h-3" />
                  </button>
                </div>

                <div className="flex justify-between items-center text-[11px]">
                  <button
                    type="button"
                    onClick={handleCaptureScreenshotAndLog}
                    className="flex items-center gap-1 text-purple-400 hover:text-purple-300 font-semibold px-2 py-1 rounded hover:bg-purple-600/20 transition-colors"
                    title="Capture current 3D view and embed direct markdown image link"
                  >
                    <Camera className="w-3 h-3" /> 📸 Screenshot & Link
                  </button>

                  <button
                    type="button"
                    onClick={handleCopyLogsForAI}
                    className="flex items-center gap-1 text-blue-400 hover:text-blue-300 font-semibold px-2 py-1 rounded hover:bg-blue-600/20 transition-colors"
                    title="Copy formatted markdown transcript to paste into chat"
                  >
                    <Copy className="w-3 h-3" /> 📋 Copy Log for AI
                  </button>
                </div>
              </form>
            </div>
          )}
        </div>

        {/* Client-side Controls Overlay */}
        <div className="absolute bottom-4 left-4 bg-[#111827]/80 backdrop-blur-md px-3 py-2 rounded-lg border border-[#1f2937] text-xs text-neutral-400 flex gap-4 select-none">
          <div><span className="font-semibold text-neutral-300">Left Click + Drag</span>: Rotate</div>
          <div><span className="font-semibold text-neutral-300">Right Click + Drag</span>: Pan</div>
          <div><span className="font-semibold text-neutral-300">Scroll</span>: Zoom</div>
        </div>
      </div>
    </div>
  );
};

export default WorldViewer;
