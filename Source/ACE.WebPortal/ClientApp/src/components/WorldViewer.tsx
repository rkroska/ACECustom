import React, { type FC, useState, useEffect, useMemo, useRef, Suspense } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
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
  AlertTriangle
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
  { wcid: 98, name: 'Olthoi Soldier' },
  { wcid: 1045, name: 'Tusker Guard' },
  { wcid: 554, name: 'Drudge Ravager' },
  { wcid: 1540, name: 'Lugian Warlord' },
];

const PRESET_PALETTES = [
  { id: 67108967, hex: '#04000097', name: 'Metallic Gold' },
  { id: 67108864, hex: '#0400005C', name: 'Shadow Obsidian' },
  { id: 67108978, hex: '#040000A2', name: 'Lightning Blue' },
  { id: 67109024, hex: '#040000D0', name: 'Flame Red' },
  { id: 67109027, hex: '#040000D3', name: 'Acid Green' },
  { id: 67109039, hex: '#040000DF', name: 'Metallic Pink' },
  { id: 67109040, hex: '#040000E0', name: 'Rusty Bronze' },
  { id: 67108973, hex: '#0400009D', name: 'Ice White' },
];

// --- 3D Model Instance with Custom WebGL Shader ---
interface ModelProps {
  wcid: number;
  paletteId: number;
  rotationSpeed: number;
  isRotating: boolean;
  wireframe: boolean;
}

const Model: FC<ModelProps> = ({ wcid, paletteId, rotationSpeed, isRotating, wireframe }) => {
  const modelUrl = `/api/visualizer/mesh/${wcid}.gltf`;
  const paletteUrl = `/api/visualizer/palette/${paletteId}.png`;

  // useGLTF suspends while parsing binary buffer
  const { scene } = useGLTF(modelUrl);
  const groupRef = useRef<THREE.Group>(null);

  // Load palette texture map (256x1 pixels)
  const paletteTexture = useMemo(() => {
    const loader = new THREE.TextureLoader();
    const tex = loader.load(paletteUrl);
    tex.minFilter = THREE.NearestFilter;
    tex.magFilter = THREE.NearestFilter;
    return tex;
  }, [paletteUrl]);

  // Apply shader to indexed meshes
  useEffect(() => {
    scene.traverse((child: any) => {
      if (child.isMesh) {
        if (child.material) {
          child.material.wireframe = wireframe;

          const isIndexed = child.material.userData?.extras?.indexed === true;
          if (isIndexed && child.material.map) {
            const indexedTexture = child.material.map;
            indexedTexture.minFilter = THREE.NearestFilter;
            indexedTexture.magFilter = THREE.NearestFilter;

            child.material = new THREE.ShaderMaterial({
              uniforms: {
                u_indexedTexture: { value: indexedTexture },
                u_paletteTexture: { value: paletteTexture }
              },
              vertexShader: `
                varying vec2 v_uv;
                void main() {
                  v_uv = uv;
                  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
                }
              `,
              fragmentShader: `
                uniform sampler2D u_indexedTexture;
                uniform sampler2D u_paletteTexture;
                varying vec2 v_uv;
                void main() {
                  float idx = texture2D(u_indexedTexture, v_uv).r;
                  float u = (idx * 255.0 + 0.5) / 256.0;
                  vec4 finalColor = texture2D(u_paletteTexture, vec2(u, 0.5));
                  if (finalColor.a < 0.1) discard;
                  gl_FragColor = finalColor;
                }
              `,
              transparent: true,
              depthWrite: true,
              side: THREE.DoubleSide
            });
          }
        }
      }
    });
  }, [scene, paletteTexture, wireframe]);

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
  const [paletteId, setPaletteId] = useState<number>(67108967); // Gold
  const [isRotating, setIsRotating] = useState<boolean>(true);
  const [rotationSpeed, setRotationSpeed] = useState<number>(0.25);
  const [wireframe, setWireframe] = useState<boolean>(false);
  const [showGrid, setShowGrid] = useState<boolean>(true);
  const [lightIntensity, setLightIntensity] = useState<number>(1.2);

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
            Active Subpalette
          </label>
          <div className="grid grid-cols-4 gap-2">
            {PRESET_PALETTES.map((pal) => (
              <button
                key={pal.id}
                title={`${pal.name} (${pal.hex})`}
                onClick={() => setPaletteId(pal.id)}
                className={`w-full aspect-square rounded-lg border-2 flex items-center justify-center transition-all ${
                  paletteId === pal.id 
                    ? 'border-blue-500 scale-105 shadow-lg shadow-blue-500/20' 
                    : 'border-[#1f2937] hover:border-neutral-500'
                }`}
                style={{ backgroundColor: pal.hex.replace('#04', '#') }}
              >
                {paletteId === pal.id && (
                  <span className="w-2.5 h-2.5 bg-[#111827] rounded-full" />
                )}
              </button>
            ))}
          </div>
          <div className="text-xs text-neutral-400 mt-1 text-center font-mono bg-[#1f2937]/50 py-1.5 rounded-md">
            Active: {PRESET_PALETTES.find(p => p.id === paletteId)?.name || 'Custom'}
          </div>
        </div>

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
                  rotationSpeed={rotationSpeed}
                  isRotating={isRotating}
                  wireframe={wireframe}
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
