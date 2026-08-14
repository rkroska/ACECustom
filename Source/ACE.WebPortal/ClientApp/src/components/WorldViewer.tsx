import React, { type FC, useState, useEffect, useRef, useMemo, Suspense } from 'react';
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
  Layers,
  Sliders,
  ThumbsUp,
  ThumbsDown,
  CheckCircle2,
  XCircle,
  MessageSquare,
  Copy,
  RotateCcw,
  Send,
  Sparkles
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
  { wcid: 17, name: 'Gromnie' },
  { wcid: 25749, name: 'Olthoi' },
  { wcid: 35427, name: 'Drudge' },
  { wcid: 18, name: 'Mattekar' },
  { wcid: 35134, name: 'Lugian' },
  { wcid: 36967, name: 'Tusker' },
  { wcid: 35146, name: 'Banderling' },
  { wcid: 420600, name: 'Viridian Statue' },
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
  creatureParticles?: any[];
  particleEffectsEnabled?: boolean;
  particleOffsets?: any;
  onCreated: (gl: any) => void;
  onMeshListLoaded?: (meshList: any[]) => void;
}

const Model: FC<ModelProps> = ({ wcid, paletteId, paletteSlot = -1, hueShift, activeTexReplaceInfo, rotationSpeed, isRotating, wireframe, creatureParticles = [], particleEffectsEnabled = true, particleOffsets, onCreated, onMeshListLoaded }) => {
  const modelUrl = `/api/visualizer/mesh/${wcid}.gltf?paletteId=${paletteId || 0}&hue=${hueShift || 0}&slot=${paletteSlot}&v=4.0-clean-bake-v1`;

  // useGLTF suspends while parsing binary buffer
  const { scene } = useGLTF(modelUrl);
  const { gl } = useThree();

  // Expose GL context to parent for screenshots
  useEffect(() => {
    if (gl && onCreated) {
      onCreated(gl);
    }
  }, [gl, onCreated]);

  // Ensure standard materials and apply wireframe
  useEffect(() => {
    const parts: any[] = [];
    let idxCounter = 0;

    scene.traverse((child: any) => {
      if (child.isMesh && child.material) {
        const matList = Array.isArray(child.material) ? child.material : [child.material];
        matList.forEach((mat: any) => {
          if (mat.map) {
            mat.map.wrapS = THREE.RepeatWrapping;
            mat.map.wrapT = THREE.RepeatWrapping;
            mat.map.minFilter = THREE.NearestFilter;
            mat.map.magFilter = THREE.NearestFilter;
            mat.map.needsUpdate = true;
            if (!mat.userData.originalMap) {
              mat.userData.originalMap = mat.map;
            }
          }

          const matName = (mat.name || '').toUpperCase();
          const mapSrc = (mat.map?.image?.src || mat.userData?.originalMap?.image?.src || '').toUpperCase();
          const combined = matName + ' ' + mapSrc;

          const extras = mat.userData?.gltfExtensions?.extras || mat.userData?.extras || {};
          const datTranslucency = extras.translucency ?? 0;

          // 0500 SurfaceTextures are standard texture surfaces
          const isSurfaceTexture = combined.includes('0500');
          const isAdditiveEnergy = datTranslucency > 0 || 
                                   combined.includes('0500303D') || 
                                   combined.includes('05003305');

          if (mat.userData.customBlending !== undefined) {
            if (mat.userData.customBlending === 'additive') {
              mat.transparent = true;
              mat.blending = THREE.AdditiveBlending;
              mat.depthWrite = false;
            } else if (mat.userData.customBlending === 'opaque') {
              mat.transparent = false;
              mat.blending = THREE.NormalBlending;
              mat.depthWrite = true;
            } else if (mat.userData.customBlending === 'blend') {
              mat.transparent = true;
              mat.blending = THREE.NormalBlending;
              mat.depthWrite = true;
            }
          } else if (isAdditiveEnergy) {
            mat.transparent = true;
            mat.blending = THREE.AdditiveBlending;
            mat.depthWrite = false;
            mat.side = THREE.DoubleSide;
          } else if (isSurfaceTexture) {
            mat.transparent = false; // Keep false to avoid see-through alpha sorting holes
            mat.alphaTest = 0.5; // Fix: Cull transparent background pixels (e.g. Drudge Lurker orange neck)
            mat.blending = THREE.NormalBlending;
            mat.depthWrite = true;
            mat.side = THREE.DoubleSide;
          } else {
            mat.transparent = false; // DAT Translucency = 0 -> Solid Opaque!
            mat.blending = THREE.NormalBlending;
            mat.depthWrite = true;
            mat.side = THREE.DoubleSide;
          }

          mat.wireframe = wireframe;
          mat.needsUpdate = true;

          idxCounter++;
          parts.push({
            partIndex: idxCounter - 1,
            meshName: child.name || `Mesh_${idxCounter}`,
            matName: mat.name || `Material_${idxCounter}`,
            mapSrc: mat.map?.image?.src || mat.userData?.originalMap?.image?.src || '',
            transparent: mat.transparent,
            blendingMode: mat.userData.customBlending || (mat.blending === THREE.AdditiveBlending ? 'additive' : mat.transparent ? 'blend' : 'opaque'),
            matRef: mat,
            meshRef: child
          });
        });
      }
    });

    if (onMeshListLoaded && parts.length > 0) {
      onMeshListLoaded(parts);
    }
  }, [scene, wireframe, onMeshListLoaded]);

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
                const newUrl = `/api/visualizer/texture/${newHex}.png?wcid=${wcid}&paletteId=${paletteId || 0}&hue=${hueShift || 0}&slot=${paletteSlot}&t=${Date.now()}`;

                new THREE.TextureLoader().load(newUrl, (tex) => {
                  tex.flipY = false;
                  tex.minFilter = THREE.NearestFilter;
                  tex.magFilter = THREE.NearestFilter;
                  tex.needsUpdate = true;

                  if (mat.userData.customTexture && mat.userData.customTexture !== tex) {
                    mat.userData.customTexture.dispose();
                  }
                  mat.userData.customTexture = tex;

                  mat.map = tex;
                  if (mat.map) mat.map.needsUpdate = true;
                  mat.needsUpdate = true;
                });
              }
            } else if ((paletteId && paletteId > 0) || (hueShift && hueShift !== 0)) {
              const matName = mat.name || '';
              const mapSrc = mat.map?.image?.src || mat.userData.originalMap?.image?.src || '';
              const match = (matName + ' ' + mapSrc).match(/0[568][0-9A-Fa-f]{6}/i);
              if (match) {
                const texIdHex = match[0].toUpperCase();
                const newUrl = `/api/visualizer/texture/${texIdHex}.png?wcid=${wcid}&paletteId=${paletteId || 0}&hue=${hueShift || 0}&slot=${paletteSlot}&t=${Date.now()}`;

                new THREE.TextureLoader().load(newUrl, (tex) => {
                  tex.flipY = false;
                  tex.wrapS = THREE.RepeatWrapping;
                  tex.wrapT = THREE.RepeatWrapping;
                  tex.minFilter = THREE.NearestFilter;
                  tex.magFilter = THREE.NearestFilter;
                  tex.needsUpdate = true;

                  if (mat.userData.customTexture && mat.userData.customTexture !== tex) {
                    mat.userData.customTexture.dispose();
                  }
                  mat.userData.customTexture = tex;

                  mat.map = tex;
                  if (mat.color) mat.color.setHex(0xffffff);
                  if (mat.map) mat.map.needsUpdate = true;
                  mat.needsUpdate = true;
                });
              }
            } else if (mat.userData.originalMap) {
              if (mat.userData.customTexture) {
                mat.userData.customTexture.dispose();
                mat.userData.customTexture = null;
              }
              mat.map = mat.userData.originalMap;
              mat.needsUpdate = true;
            }
          }
        });
      }
    });
  }, [scene, activeTexReplaceInfo, wcid, paletteId, hueShift, paletteSlot]);

  // Unmount WebGL GPU disposal cleanup
  useEffect(() => {
    return () => {
      scene.traverse((child: any) => {
        if (child.isMesh) {
          const matList = Array.isArray(child.material) ? child.material : [child.material];
          matList.forEach((mat: any) => {
            if (mat.userData?.customTexture) {
              mat.userData.customTexture.dispose();
            }
            mat.dispose();
          });
        }
      });
    };
  }, [scene]);

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

  return (
    <CanvasModel
      scene={scene}
      isRotating={isRotating}
      rotationSpeed={rotationSpeed}
      wireframe={wireframe}
      activeTexReplaceInfo={activeTexReplaceInfo}
      wcid={wcid}
      paletteId={paletteId || 0}
      hueShift={hueShift || 0}
      paletteSlot={paletteSlot}
      onMeshListLoaded={onMeshListLoaded}
      particleEffectsEnabled={particleEffectsEnabled}
      creatureParticles={creatureParticles}
      particleOffsets={particleOffsets}
    />
  );
};

interface CanvasModelProps {
  scene: THREE.Object3D;
  isRotating: boolean;
  rotationSpeed: number;
  wireframe: boolean;
  activeTexReplaceInfo: any;
  wcid: number;
  paletteId: number;
  hueShift: number;
  paletteSlot: number;
  onMeshListLoaded?: (parts: any[]) => void;
  particleEffectsEnabled: boolean;
  creatureParticles?: any[];
  particleOffsets?: any;
}

const CanvasModel: FC<CanvasModelProps> = ({
  scene,
  isRotating,
  rotationSpeed,
  wireframe: _wireframe,
  activeTexReplaceInfo: _activeTexReplaceInfo,
  wcid,
  paletteId: _paletteId,
  hueShift: _hueShift,
  paletteSlot: _paletteSlot,
  onMeshListLoaded: _onMeshListLoaded,
  particleEffectsEnabled,
  creatureParticles,
  particleOffsets
}) => {
  const groupRef = useRef<THREE.Group>(null);

  // Auto-rotate model if enabled
  useFrame((_state, delta) => {
    if (isRotating && groupRef.current) {
      groupRef.current.rotation.y += delta * rotationSpeed;
    }
  });

  return (
    <group ref={groupRef}>
      <primitive object={scene} />
      {particleEffectsEnabled && (
        (creatureParticles && creatureParticles.length > 0
          ? creatureParticles
          : (wcid === 420600 ? [{ emitterId: 0x3200011E, partIndex: 14, emitter: null }] : [])
        ).map((hook: any, idx: number) => (
          <ParticleEmitterInstance
            key={`${hook.emitterId}_${hook.partIndex}_${idx}`}
            hookInfo={hook}
            parentScene={scene}
            enabled={particleEffectsEnabled}
            particleOffsets={particleOffsets}
            wcid={wcid}
          />
        ))
      )}
    </group>
  );
};

// --- Circular Glow Canvas Texture Generator Fallback ---
const createCircularGlowTexture = (): THREE.CanvasTexture => {
  const canvas = document.createElement('canvas');
  canvas.width = 64;
  canvas.height = 64;
  const ctx = canvas.getContext('2d');
  if (ctx) {
    const gradient = ctx.createRadialGradient(32, 32, 0, 32, 32, 32);
    gradient.addColorStop(0, 'rgba(255, 255, 255, 1)');
    gradient.addColorStop(0.2, 'rgba(255, 60, 60, 0.95)');
    gradient.addColorStop(0.6, 'rgba(220, 20, 20, 0.45)');
    gradient.addColorStop(1, 'rgba(0, 0, 0, 0)');
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, 64, 64);
  }
  const tex = new THREE.CanvasTexture(canvas);
  tex.needsUpdate = true;
  return tex;
};

// --- Reusable WebGL Particle Emitter Component ---
interface ParticleEmitterProps {
  hookInfo: any;
  parentScene: THREE.Object3D;
  enabled: boolean;
  particleOffsets?: any;
  wcid?: number;
}

const ParticleEmitterInstance: FC<ParticleEmitterProps> = ({ hookInfo, parentScene: _parentScene, enabled, particleOffsets, wcid }) => {
  const pointsRef = useRef<THREE.Points>(null);
  const coreRef = useRef<THREE.Mesh>(null);
  const textureRef = useRef<THREE.Texture | null>(null);
  const geometryRef = useRef<THREE.BufferGeometry | null>(null);
  const materialRef = useRef<THREE.PointsMaterial | null>(null);

  const emitter = hookInfo.emitter;
  const isRynthid = (wcid === 420600);

  const isBottomFireTail = isRynthid && (hookInfo.emitterId === 0x32000120 || 
                            hookInfo.emitterId === 838861088 || 
                            String(hookInfo.emitterId).includes('32000120') || 
                            (hookInfo.hexId && String(hookInfo.hexId).toLowerCase().includes('32000120')));

  const isChestOrb = isRynthid && ((hookInfo.emitterId === 0x3200011E || 
                       hookInfo.emitterId === 838861086 || 
                       String(hookInfo.emitterId).includes('3200011e') || 
                       (hookInfo.hexId && String(hookInfo.hexId).toLowerCase().includes('3200011e'))) && !isBottomFireTail);

  const isWingParticle = hookInfo.partIndex >= 16 && hookInfo.partIndex <= 19;
  const particleColor = isWingParticle ? '#ff00cc' : isBottomFireTail ? '#ff6600' : '#ff0033';
  const maxParticles = isBottomFireTail ? 90 : Math.min(emitter?.maxParticles || 40, 150);

  // Particle state arrays
  const particleState = useRef<{
    positions: Float32Array;
    velocities: Float32Array;
    lifetimes: Float32Array;
    maxLifetimes: Float32Array;
  }>({
    positions: new Float32Array(maxParticles * 3),
    velocities: new Float32Array(maxParticles * 3),
    lifetimes: new Float32Array(maxParticles),
    maxLifetimes: new Float32Array(maxParticles)
  });

  useEffect(() => {
    textureRef.current = createCircularGlowTexture();

    const geometry = new THREE.BufferGeometry();
    const positions = new Float32Array(maxParticles * 3);
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geometryRef.current = geometry;

    const material = new THREE.PointsMaterial({
      size: isWingParticle ? 0.25 : isBottomFireTail ? 0.45 : 0.35,
      map: textureRef.current,
      transparent: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      color: new THREE.Color(particleColor)
    });
    materialRef.current = material;

    for (let i = 0; i < maxParticles; i++) {
      particleState.current.lifetimes[i] = Math.random() * (emitter?.lifespan || 1.5);
      particleState.current.maxLifetimes[i] = (emitter?.lifespan || 1.5) + (Math.random() - 0.5) * (emitter?.lifespanRand || 0.4);
    }

    return () => {
      if (geometryRef.current) geometryRef.current.dispose();
      if (materialRef.current) materialRef.current.dispose();
      if (textureRef.current) textureRef.current.dispose();
    };
  }, [maxParticles, emitter, isWingParticle, isBottomFireTail, particleColor]);

  // Programmatically attach particle emitters directly to their exact DAT bone nodes
  useEffect(() => {
    if (!_parentScene || !coreRef.current) return;

    let targetNode: THREE.Object3D | null = null;
    let targetPartName = `Part_${hookInfo.partIndex}_`;

    if (isChestOrb || isBottomFireTail) {
      // Both Chest Orb and Fire Tail attach directly to Part 20 (Chest Armor Collar) to lock Z=0.00 center-line
      targetPartName = 'Part_20_';
    }

    _parentScene.traverse((child: any) => {
      if (!targetNode && child.name && child.name.includes(targetPartName)) {
        targetNode = child;
      }
    });

    if (targetNode && coreRef.current) {
      (targetNode as THREE.Object3D).add(coreRef.current);
      coreRef.current.position.set(0, 0, 0);
    }

    return () => {
      if (targetNode && coreRef.current) {
        (targetNode as THREE.Object3D).remove(coreRef.current);
      }
    };
  }, [_parentScene, hookInfo.partIndex, isWingParticle, isChestOrb, isBottomFireTail]);

  // Pulse effect and particle position relative to Chest Armor Collar (Part 20)
  useFrame((_state, delta) => {
    if (!enabled || !pointsRef.current || !coreRef.current) return;

    if (isChestOrb) {
      const fx = particleOffsets?.chestOrbX ?? 0.00;
      const fy = particleOffsets?.chestOrbY ?? 0.00;
      const fz = particleOffsets?.chestOrbZ ?? 0.00;
      coreRef.current.position.set(fx, fy, fz);
      const pulse = 1.0 + Math.sin(Date.now() * 0.005) * 0.15;
      coreRef.current.scale.set(pulse, pulse, pulse);
    } else if (isBottomFireTail) {
      const fx = particleOffsets?.fireTailX ?? 0.00;
      const fy = particleOffsets?.fireTailY ?? 0.00;
      const fz = particleOffsets?.fireTailZ ?? 0.00;
      coreRef.current.position.set(fx, fy, fz);
    }

    const posAttr = pointsRef.current?.geometry?.attributes?.position || geometryRef.current?.attributes?.position;
    if (!posAttr || !posAttr.array) return;
    const positions = posAttr.array as Float32Array;
    const state = particleState.current;

    for (let i = 0; i < maxParticles; i++) {
      state.lifetimes[i] += delta;
      if (state.lifetimes[i] >= state.maxLifetimes[i]) {
        state.lifetimes[i] = 0;
        const idx = i * 3;
        positions[idx] = (Math.random() - 0.5) * (isWingParticle ? 0.18 : isBottomFireTail ? 0.35 : 0.08);
        positions[idx + 1] = (Math.random() - 0.5) * (isWingParticle ? 0.18 : isBottomFireTail ? 0.35 : 0.08);
        positions[idx + 2] = (Math.random() - 0.5) * (isWingParticle ? 0.18 : isBottomFireTail ? 0.35 : 0.08);

        state.velocities[idx] = (Math.random() - 0.5) * (isBottomFireTail ? 0.10 : 0.04);
        state.velocities[idx + 1] = (isWingParticle ? -0.01 : isBottomFireTail ? -0.75 : 0.02) + (Math.random() - 0.5) * 0.06;
        state.velocities[idx + 2] = (Math.random() - 0.5) * (isBottomFireTail ? 0.10 : 0.04);
      } else {
        const idx = i * 3;
        positions[idx] += state.velocities[idx] * delta;
        positions[idx + 1] += state.velocities[idx + 1] * delta;
        positions[idx + 2] += state.velocities[idx + 2] * delta;
      }
    }

    if (pointsRef.current?.geometry?.attributes?.position) {
      pointsRef.current.geometry.attributes.position.needsUpdate = true;
    }
  });

  if (!enabled || (!isChestOrb && !isBottomFireTail && !isWingParticle) || !geometryRef.current || !materialRef.current) return null;
  if (isChestOrb && particleOffsets?.showChestOrb === false) return null;
  if (isBottomFireTail && particleOffsets?.showFireTail === false) return null;
  if (isWingParticle && particleOffsets?.showWings === false) return null;

  return (
    <group ref={coreRef}>
      {/* Dynamic 3D Mesh Elements, Flame Core, & Lights matching AC client */}
      {isBottomFireTail ? (
        <>
          {/* Deep Orange Energy Plasma Stream Lights (Matching in-game plasma column) */}
          <pointLight color="#ff4400" intensity={1.8} distance={1.2} position={[particleOffsets?.fireTailX ?? 0.27, particleOffsets?.fireTailY ?? -0.39, particleOffsets?.fireTailZ ?? 0]} />
        </>
      ) : isChestOrb ? (
        <>
          {/* Soft Orange/Red Core inside Chest Collar */}
          <mesh>
            <sphereGeometry args={[0.035, 16, 16]} />
            <meshBasicMaterial color="#ffffff" />
          </mesh>
          <mesh>
            <sphereGeometry args={[0.08, 16, 16]} />
            <meshBasicMaterial
              color="#ff2200"
              transparent
              blending={THREE.AdditiveBlending}
              depthWrite={false}
              opacity={0.80}
            />
          </mesh>
          <pointLight color="#ff2200" intensity={0.8} distance={0.6} position={[particleOffsets?.chestOrbX ?? 0.29, particleOffsets?.chestOrbY ?? 0.22, particleOffsets?.chestOrbZ ?? -0.2]} />

          {/* Magenta Head Glow */}
          {particleOffsets?.showHeadAura && (
            <pointLight color="#ff00cc" intensity={1.5} distance={1.0} position={[0, 0.4, -0.2]} />
          )}
        </>
      ) : null}

      {/* Ambient Particle Cloud - Suppressed for wings to prevent magenta light/bubble spill */}
      {!isWingParticle && (
        <points ref={pointsRef} geometry={geometryRef.current} material={materialRef.current} />
      )}
    </group>
  );
};

// --- Main WorldViewer Page ---
interface WorldViewerProps {
  wcid?: number;
  paletteOverride?: number;
  hueShiftOverride?: number;
  compactMode?: boolean;
}

const WorldViewer: FC<WorldViewerProps> = ({ wcid: propWcid, paletteOverride: propPaletteOverride, hueShiftOverride: propHueShift, compactMode = false }) => {
  const [wcid, setWcid] = useState<number>(propWcid || 420600); // Default Rynthid Nullifier
  const [searchInput, setSearchInput] = useState<string>((propWcid || 420600).toString());
  const [paletteId, setPaletteId] = useState<number>(propPaletteOverride || 0); 
  const [hueShift, setHueShift] = useState<number>(propHueShift || 0);

  useEffect(() => {
    if (propWcid) {
      setWcid(propWcid);
      setSearchInput(propWcid.toString());
    }
  }, [propWcid]);

  useEffect(() => {
    if (propPaletteOverride !== undefined) {
      setPaletteId(propPaletteOverride);
    }
  }, [propPaletteOverride]);

  useEffect(() => {
    if (propHueShift !== undefined) {
      setHueShift(propHueShift);
    }
  }, [propHueShift]);
  const [speciesPalettes, setSpeciesPalettes] = useState<any[]>([]);
  const [textureReplacements, setTextureReplacements] = useState<any[]>([]);
  const [activeTexReplaceIdx, setActiveTexReplaceIdx] = useState<number>(-1);

  // Smart Palette States
  const [smartPalettes, setSmartPalettes] = useState<any[]>([]);
  const [smartFamily, setSmartFamily] = useState<string>('all');
  const [paletteSlot, setPaletteSlot] = useState<number>(-1);
  const [minConfidenceScore, setMinConfidenceScore] = useState<number>(0);
  const [curationQueueTab, setCurationQueueTab] = useState<'unrated' | 'approved' | 'blacklisted' | 'all'>('unrated');
  const [copiedCreateCmd, setCopiedCreateCmd] = useState<boolean>(false);
  const [copiedSetCmd, setCopiedSetCmd] = useState<boolean>(false);
  // Mesh Inspector States
  const [meshParts, setMeshParts] = useState<any[]>([]);
  const [showMeshInspector, setShowMeshInspector] = useState<boolean>(false);
  const [isCompareModalOpen, setIsCompareModalOpen] = useState<boolean>(false);
  const [comparePartAIdx, setComparePartAIdx] = useState<number>(0);
  const [comparePartBIdx, setComparePartBIdx] = useState<number>(1);
  const [copiedCompareText, setCopiedCompareText] = useState<string | null>(null);

  // New Ergonomic Sidebar & Search States
  const [sidebarTab, setSidebarTab] = useState<'palettes' | 'textures' | 'catalog'>('palettes');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [searchResults, setSearchResults] = useState<any[]>([]);
  const [isSearching, setIsSearching] = useState<boolean>(false);
  const [ratingNote, setRatingNote] = useState<string>('');

  // Saved Catalog Items (localStorage backed)
  interface SavedCatalogItem {
    id: string;
    wcid: number;
    creatureName: string;
    paletteId: number;
    paletteTemplate?: number;
    paletteHex: string;
    rating: 'approved' | 'denied';
    swatches: string[];
    note: string;
    date: string;
  }

  const [savedCatalog, setSavedCatalog] = useState<SavedCatalogItem[]>(() => {
    try {
      const saved = localStorage.getItem('ace_showroom_catalog');
      return saved ? JSON.parse(saved) : [];
    } catch {
      return [];
    }
  });

  const [savedFilter, setSavedFilter] = useState<'all' | 'approved' | 'denied'>('all');

  useEffect(() => {
    try {
      localStorage.setItem('ace_showroom_catalog', JSON.stringify(savedCatalog));
    } catch (e) {
      console.error('Failed to save showroom catalog to localStorage', e);
    }
  }, [savedCatalog]);

  // Debounced Creature Search (Name OR WCID)
  useEffect(() => {
    if (!searchQuery.trim()) {
      setSearchResults([]);
      setIsSearching(false);
      return;
    }

    setIsSearching(true);
    const timer = setTimeout(() => {
      fetch(`/api/visualizer/search-creatures?query=${encodeURIComponent(searchQuery.trim())}`)
        .then(r => r.json())
        .then(data => {
          if (Array.isArray(data)) setSearchResults(data);
          else setSearchResults([]);
          setIsSearching(false);
        })
        .catch(() => {
          setSearchResults([]);
          setIsSearching(false);
        });
    }, 250);

    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Live 3D Particle Fine-Tuner States (DAT Bone Node Relative)
  const [chestOrbX, setChestOrbX] = useState<number>(0.00);
  const [chestOrbY, setChestOrbY] = useState<number>(0.16);
  const [chestOrbZ, setChestOrbZ] = useState<number>(0.00);

  const [fireTailX, setFireTailX] = useState<number>(0.00);
  const [fireTailY, setFireTailY] = useState<number>(-0.55);
  const [fireTailZ, setFireTailZ] = useState<number>(0.25);

  const [wingsCenterX, setWingsCenterX] = useState<number>(0.00);
  const [wingsX, setWingsX] = useState<number>(0.88);
  const [wingsY, setWingsY] = useState<number>(0.45);
  const [wingsZ, setWingsZ] = useState<number>(-0.23);

  const [showHeadAura, setShowHeadAura] = useState<boolean>(false);
  const [showChestOrb, setShowChestOrb] = useState<boolean>(true);
  const [showFireTail, setShowFireTail] = useState<boolean>(true);
  const [showWings, setShowWings] = useState<boolean>(false);
  const [showTailConeMesh, setShowTailConeMesh] = useState<boolean>(false);
  const [activeParticleTab, setActiveParticleTab] = useState<'chest' | 'fire' | 'wings'>('chest');

  const handleTogglePartVisibility = (partIndex: number) => {
    setMeshParts(prev => prev.map((part, idx) => {
      if (idx === partIndex && part.meshRef) {
        const nextVis = part.visible === undefined ? false : !part.visible;
        part.meshRef.visible = nextVis;
        return { ...part, visible: nextVis };
      }
      return part;
    }));
  };

  const handleIsolatePart = (partIndex: number) => {
    setMeshParts(prev => prev.map((part, idx) => {
      if (part.meshRef) {
        const isTarget = idx === partIndex;
        part.meshRef.visible = isTarget;
        return { ...part, visible: isTarget };
      }
      return part;
    }));
  };

  const handleShowAllParts = () => {
    setMeshParts(prev => prev.map(part => {
      if (part.meshRef) {
        part.meshRef.visible = true;
        return { ...part, visible: true };
      }
      return part;
    }));
  };

  const handleToggleBlending = (partIndex: number, newMode: 'opaque' | 'additive' | 'blend') => {
    setMeshParts(prev => prev.map((part, idx) => {
      if (idx === partIndex) {
        if (part.matRef) {
          part.matRef.userData.customBlending = newMode;
          if (newMode === 'additive') {
            part.matRef.transparent = true;
            part.matRef.blending = THREE.AdditiveBlending;
            part.matRef.depthWrite = false;
          } else if (newMode === 'opaque') {
            part.matRef.transparent = false;
            part.matRef.blending = THREE.NormalBlending;
            part.matRef.depthWrite = true;
          } else if (newMode === 'blend') {
            part.matRef.transparent = true;
            part.matRef.blending = THREE.NormalBlending;
            part.matRef.depthWrite = true;
          }
          part.matRef.needsUpdate = true;
        }
        return { ...part, blendingMode: newMode };
      }
      return part;
    }));
  };


  const [copiedDebug, setCopiedDebug] = useState<boolean>(false);

  const handleCopyDebugInfo = () => {
    const creatureName = PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`;
    const logLines = [
      `# 🎨 3D Showroom Session Audit & Comments`,
      `**Creature**: ${creatureName} (WCID ${wcid})`,
      `**Date**: ${new Date().toLocaleString()}`,
      `---`,
      ``,
      targetSurfaceId ? `[${new Date().toLocaleTimeString()}] 🎯 Selected Target Surface: Surface 0x${targetSurfaceId.toString(16).toUpperCase().padStart(8, '0')}` : `[${new Date().toLocaleTimeString()}] 🎯 Target Surface: None Selected`,
      ``,
      `### 📊 Creature Surface & Texture Inventory:`,
      ...(creatureSurfaces.length > 0 ? creatureSurfaces.map(s => `- Surface 0x${(s.surfaceId || 0).toString(16).toUpperCase().padStart(8, '0')} | Texture: 0x${(s.textureId || 0).toString(16).toUpperCase().padStart(8, '0')} | Slot: ${s.paletteSlot ?? -1}`) : ['- No dynamic surfaces registered']),
      ``,
      `### 🧩 Loaded Body Parts Breakdown (${meshParts.length} Parts):`,
      ...(meshParts.length > 0 
        ? meshParts.map(p => `- Part #${p.partIndex}: ${p.meshName || 'Mesh'} | Surface: ${p.surfaceId ? '0x' + p.surfaceId.toString(16).toUpperCase() : 'N/A'} | OrigTexture: ${p.origTextureId || 'N/A'} | Vis: ${p.visible !== false ? 'ON' : 'OFF'}`)
        : ['- No body parts loaded']),
      ``,
      `### 📸 Debug JSON Dump:`,
      `\`\`\`json`,
      JSON.stringify({
        timestamp: new Date().toISOString(),
        wcid,
        targetSurfaceId: targetSurfaceId ? `0x${targetSurfaceId.toString(16).toUpperCase().padStart(8, '0')}` : null,
        particleEffectsEnabled,
        creatureParticlesCount: creatureParticles?.length || 0,
        creatureParticlesData: creatureParticles,
        meshPartsCount: meshParts.length,
        surfacesCount: creatureSurfaces.length,
        userAgent: navigator.userAgent
      }, null, 2),
      `\`\`\``
    ].join('\n');

    navigator.clipboard.writeText(logLines);
    setCopiedDebug(true);
    setTimeout(() => setCopiedDebug(false), 3000);
  };

  const [creatureParticles, setCreatureParticles] = useState<any[]>([]);
  const [particleEffectsEnabled, setParticleEffectsEnabled] = useState<boolean>(true);

  const [isRotating, setIsRotating] = useState<boolean>(false);
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
  const [speciesPresets, setSpeciesPresets] = useState<any[]>([]);
  const [creatureSurfaces, setCreatureSurfaces] = useState<any[]>([]);
  const [targetSurfaceId, setTargetSurfaceId] = useState<number>(0);
  const [textureLibrary, setTextureLibrary] = useState<any[]>([]);
  const [selectedLibTexId, setSelectedLibTexId] = useState<number>(0);
  const [customTexHex, setCustomTexHex] = useState<string>('');
  const [customPalSetHex, setCustomPalSetHex] = useState<string>('');
  const [similarPalettes, setSimilarPalettes] = useState<any[]>([]);
  const [similarTextures, setSimilarTextures] = useState<any[]>([]);

  useEffect(() => {
    fetch('/api/visualizer/species-presets')
      .then(r => r.json())
      .then(data => {
        if (Array.isArray(data) && data.length > 0) setSpeciesPresets(data);
      })
      .catch(() => {});
  }, []);

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
    const currentTex = targetSurfaceId || selectedLibTexId || 0;
    if (currentTex > 0) {
      fetch(`/api/visualizer/similar-textures/${currentTex}`)
        .then(r => r.json())
        .then(data => {
          if (Array.isArray(data)) setSimilarTextures(data);
        })
        .catch(() => setSimilarTextures([]));
    }
  }, [targetSurfaceId, selectedLibTexId]);

  useEffect(() => {
    // Reset state immediately so old creature particle effects don't linger while fetching new WCID
    setCreatureParticles([]);
    setSpeciesPalettes([]);
    setTextureReplacements([]);
    setCreatureSurfaces([]);

    fetch(`/api/visualizer/species-palettes/${wcid}`)
      .then(r => r.json())
      .then(data => {
         setSpeciesPalettes(data);
         if (propPaletteOverride === undefined) {
           const defVar = data.find((v: any) => v.isDefault);
           if (defVar) setPaletteId(defVar.templateId);
           else if (data.length > 0) setPaletteId(data[0].templateId);
           else setPaletteId(0);
           setHueShift(0);
         }
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

    fetch(`/api/visualizer/creature-particles/${wcid}`)
      .then(r => r.json())
      .then(data => {
        if (Array.isArray(data)) setCreatureParticles(data);
        else setCreatureParticles([]);
      })
      .catch(() => setCreatureParticles([]));

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
  const [isLogOpen, setIsLogOpen] = useState<boolean>(false);
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

  const handleRateVariant = (rating: 'approved' | 'denied') => {
    const activeVariant = speciesPalettes.find(p => p.templateId === paletteId || p.paletteId === paletteId);
    const creatureName = PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`;
    const palHex = `0x${(paletteId || 0).toString(16).toUpperCase().padStart(8, '0')}`;

    const newItem = {
      id: `${wcid}_${paletteId}_${Date.now()}`,
      wcid,
      creatureName,
      paletteId,
      paletteTemplate: activeVariant?.templateId,
      paletteHex: activeVariant?.paletteHex || palHex,
      rating,
      swatches: activeVariant?.swatches || [],
      note: ratingNote.trim() || (rating === 'approved' ? 'Approved Variant' : 'Denied Variant'),
      date: new Date().toLocaleDateString()
    };

    setSavedCatalog(prev => [newItem, ...prev.filter(item => !(item.wcid === wcid && item.paletteId === paletteId))]);
    setRatingNote('');
    appendLog('user', `${rating === 'approved' ? '👍 Approved (Cool)' : '👎 Denied (Ugly)'} variant ${activeVariant?.name || palHex} for ${creatureName}`);
  };

  const handleRemoveFromCatalog = (id: string) => {
    setSavedCatalog(prev => prev.filter(item => item.id !== id));
  };

  useEffect(() => {
    if (isLogOpen) {
      logEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [sessionLogs, isLogOpen]);

  // Auto-log Hue Shift changes with 400ms debounce
  useEffect(() => {
    if (hueShift === 0) return;
    const timer = setTimeout(() => {
      appendLog('auto', `🌈 Adjusted Hue Shift to ${hueShift}°`);
    }, 400);
    return () => clearTimeout(timer);
  }, [hueShift]);

  // Auto-log Target Surface selection
  useEffect(() => {
    if (targetSurfaceId !== 0) {
      const surfName = creatureSurfaces.find(s => s.textureId === targetSurfaceId)?.name || `0x${targetSurfaceId.toString(16).toUpperCase().padStart(8, '0')}`;
      appendLog('auto', `🎯 Selected Target Surface: ${surfName}`);
    }
  }, [targetSurfaceId]);

  // Auto-log Texture Replacement selection
  useEffect(() => {
    if (activeTexReplaceIdx >= 0 && textureReplacements[activeTexReplaceIdx]) {
      const item = textureReplacements[activeTexReplaceIdx];
      appendLog('auto', `⚡ Applied Texture Replacement: ${item.name}`);
    }
  }, [activeTexReplaceIdx]);

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
    const cName = PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`;
    const palHex = paletteId ? `0x${paletteId.toString(16).toUpperCase()}` : '0 (Default)';
    
    const header = [
      `# 🚀 [HOT-RELOAD VERIFIED v2.5] 3D Showroom Session Audit & Material Texture Breakdown`,
      `**Creature**: ${cName} (WCID ${wcid}) | **Active Palette**: ${palHex}`,
      `**Build Stamp**: ${new Date().toISOString()}`,
      `---`,
      ``,
      targetSurfaceId ? `[${new Date().toLocaleTimeString()}] 🎯 Selected Target Surface: Surface 0x${targetSurfaceId.toString(16).toUpperCase().padStart(8, '0')}` : `[${new Date().toLocaleTimeString()}] 🎯 Target Surface: None Selected`,
      ``,
      `### 🧩 Loaded Body Parts Breakdown (${meshParts.length} Parts):`,
      ...(meshParts.length > 0 
        ? meshParts.map(p => {
            const extractHex = (name: string) => {
              const match = (name || '').match(/0[568][0-9A-Fa-f]{6}/);
              return match ? `0x${match[0].toUpperCase()}` : 'N/A';
            };
            const texDid = extractHex(p.matName || p.mapSrc || '');
            return `- Part #${p.partIndex}: Mesh="${p.meshName || 'Mesh'}" | Mat="${p.matName || 'Mat'}" | TextureDID=${texDid} | Blending=${p.blendingMode || 'opaque'} | Trans=${p.transparent ? 'YES' : 'NO'} | Vis=${p.visible !== false ? 'ON' : 'OFF'}`;
          })
        : ['- No body parts loaded']),
      ``,
      `### 📊 Creature Surface & Texture Inventory:`,
      ...(creatureSurfaces.length > 0 ? creatureSurfaces.map(s => `- Surface 0x${(s.surfaceId || 0).toString(16).toUpperCase().padStart(8, '0')} | Texture: 0x${(s.textureId || 0).toString(16).toUpperCase().padStart(8, '0')} | Slot: ${s.paletteSlot ?? -1}`) : ['- No dynamic surfaces registered']),
      ``,
      `### 💬 Session Audit Log:`,
      ...(sessionLogs.length > 0 ? sessionLogs.map(log => log.type === 'user' ? `- User: ${log.content}` : `- ${log.timestamp} ${log.content}`) : ['- No chat entries']),
      ``,
      `### 📸 Debug JSON Dump:`,
      `\`\`\`json`,
      JSON.stringify({
        buildVersion: "2.5-hotreload-verified",
        timestamp: new Date().toISOString(),
        wcid,
        paletteId: palHex,
        targetSurfaceId: targetSurfaceId ? `0x${targetSurfaceId.toString(16).toUpperCase().padStart(8, '0')}` : null,
        meshPartsCount: meshParts.length,
        surfacesCount: creatureSurfaces.length,
        parts: meshParts.map(p => ({
          index: p.partIndex,
          mesh: p.meshName,
          mat: p.matName,
          mapSrc: p.mapSrc,
          blending: p.blendingMode,
          transparent: p.transparent
        })),
        userAgent: navigator.userAgent
      }, null, 2),
      `\`\`\``
    ].join('\n');

    await navigator.clipboard.writeText(header);
    setCopiedDebug(true);
    setTimeout(() => setCopiedDebug(false), 3000);
  };

  const getActivePaletteParamString = () => {
    if (!paletteId || paletteId === 0) return '0';
    const matchedVariant = speciesPalettes.find(p => p.templateId === paletteId || p.paletteId === paletteId);
    if (matchedVariant && matchedVariant.paletteId && (matchedVariant.paletteId & 0xFF000000) === 0x04000000) {
      return `0x${matchedVariant.paletteId.toString(16).toUpperCase()}`;
    }
    if (matchedVariant) {
      return `${matchedVariant.templateId}`;
    }
    
    if ((paletteId & 0xFF000000) === 0x04000000 || (paletteId & 0xFF000000) === 0x0F000000) {
      return `0x${paletteId.toString(16).toUpperCase()}`;
    }
    return `0x${paletteId.toString(16).toUpperCase()}`;
  };

  const handleCopyCreateCmd = async () => {
    const palParam = getActivePaletteParamString();
    const cmd = `@create ${wcid} 1 ${palParam} 0.5`;
    await navigator.clipboard.writeText(cmd);
    setCopiedCreateCmd(true);
    appendLog('user', `📋 Copied in-game command to clipboard: ${cmd}`);
    setTimeout(() => setCopiedCreateCmd(false), 3000);
  };

  const handleCopySetCmd = async () => {
    const palParam = getActivePaletteParamString();
    const cmd = `@set Int PaletteTemplate ${palParam}`;
    await navigator.clipboard.writeText(cmd);
    setCopiedSetCmd(true);
    appendLog('user', `📋 Copied in-game command to clipboard: ${cmd}`);
    setTimeout(() => setCopiedSetCmd(false), 3000);
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

  const filteredSmartPalettes = useMemo(() => {
    return smartPalettes.filter((p: any) => {
      const confidence = p.confidenceScore ?? 80;
      if (confidence < minConfidenceScore) return false;

      const key = `${wcid}_${activeCurrentTexId || 0}_${p.paletteId}`;
      const rating = curations[key] ?? 0;

      if (curationQueueTab === 'unrated') return rating === 0;
      if (curationQueueTab === 'approved') return rating === 1;
      if (curationQueueTab === 'blacklisted') return rating === -1;
      return true; // 'all'
    });
  }, [smartPalettes, minConfidenceScore, curations, wcid, activeCurrentTexId, curationQueueTab]);

  const unratedCount = useMemo(() => {
    return smartPalettes.filter((p: any) => {
      if ((p.confidenceScore ?? 80) < minConfidenceScore) return false;
      const key = `${wcid}_${activeCurrentTexId || 0}_${p.paletteId}`;
      return (curations[key] ?? 0) === 0;
    }).length;
  }, [smartPalettes, minConfidenceScore, curations, wcid, activeCurrentTexId]);

  const approvedCount = useMemo(() => {
    return Object.entries(curations).filter(([k, v]) => k.startsWith(`${wcid}_`) && v === 1).length;
  }, [curations, wcid]);

  const blacklistedCount = useMemo(() => {
    return Object.entries(curations).filter(([k, v]) => k.startsWith(`${wcid}_`) && v === -1).length;
  }, [curations, wcid]);

  const cyclePalette = (direction: 1 | -1) => {
    if (!filteredSmartPalettes || filteredSmartPalettes.length === 0) return;
    const currIdx = filteredSmartPalettes.findIndex((p: any) => p.paletteId === paletteId);
    let nextIdx = 0;
    if (currIdx >= 0) {
      nextIdx = (currIdx + direction + filteredSmartPalettes.length) % filteredSmartPalettes.length;
    } else {
      nextIdx = direction === 1 ? 0 : filteredSmartPalettes.length - 1;
    }
    setPaletteId(filteredSmartPalettes[nextIdx].paletteId);
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

        // Automatically advance to the next palette in queue
        if (filteredSmartPalettes && filteredSmartPalettes.length > 0) {
          const currIdx = filteredSmartPalettes.findIndex((p: any) => p.paletteId === activePalId);
          let nextIdx = (currIdx + 1) % filteredSmartPalettes.length;
          setPaletteId(filteredSmartPalettes[nextIdx].paletteId);
        } else if (speciesPalettes && speciesPalettes.length > 0) {
          const currIdx = speciesPalettes.findIndex((sp: any) => sp.templateId === activePalId);
          let nextIdx = (currIdx + 1) % speciesPalettes.length;
          setPaletteId(speciesPalettes[nextIdx].templateId);
        }
      })
      .catch(e => console.error("Failed to submit curation: ", e));
  };

  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      const targetEl = e.target as HTMLElement;
      if (['INPUT', 'SELECT', 'TEXTAREA'].includes(targetEl?.tagName) || targetEl?.isContentEditable) return;

      if (e.key === 'ArrowRight' || e.key === 'd' || e.key === 'D') {
        e.preventDefault();
        cyclePalette(1);
      } else if (e.key === 'ArrowLeft' || e.key === 'q' || e.key === 'Q') {
        e.preventDefault();
        cyclePalette(-1);
      } else if (e.key === 'a' || e.key === 'A') {
        handleCurationSubmit(1);
      } else if (e.key === 'x' || e.key === 'X') {
        handleCurationSubmit(-1);
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [wcid, activeTexReplaceIdx, targetSurfaceId, paletteId, textureReplacements, filteredSmartPalettes, curationQueueTab]);

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

  const renderCompareModal = () => {
    if (!isCompareModalOpen) return null;

    const partA = meshParts[comparePartAIdx] || {};
    const partB = meshParts[comparePartBIdx] || {};

    const extractHex = (name: string) => {
      const match = (name || '').match(/0[568][0-9A-Fa-f]{6}/);
      return match ? `0x${match[0].toUpperCase()}` : 'N/A';
    };

    const compData = {
      Creature: PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`,
      WCID: wcid,
      PaletteID: paletteId ? `0x${paletteId.toString(16).toUpperCase()}` : '0 (Default)',
      Part_A: {
        Index: partA.partIndex ?? 'N/A',
        Mesh: partA.meshName ?? 'N/A',
        Material: partA.matName ?? 'N/A',
        Texture_DID: extractHex(partA.matName || partA.mapSrc || ''),
        Blending: partA.blendingMode ?? 'opaque',
        Transparent: partA.transparent ? 'Yes' : 'No',
        Visibility: partA.visible === false ? 'OFF' : 'ON'
      },
      Part_B: {
        Index: partB.partIndex ?? 'N/A',
        Mesh: partB.meshName ?? 'N/A',
        Material: partB.matName ?? 'N/A',
        Texture_DID: extractHex(partB.matName || partB.mapSrc || ''),
        Blending: partB.blendingMode ?? 'opaque',
        Transparent: partB.transparent ? 'Yes' : 'No',
        Visibility: partB.visible === false ? 'OFF' : 'ON'
      }
    };

    const handleCopyJson = () => {
      const jsonStr = JSON.stringify(compData, null, 2);
      navigator.clipboard.writeText(jsonStr);
      setCopiedCompareText("Copied JSON!");
      setTimeout(() => setCopiedCompareText(null), 2500);
    };

    const handleCopyMarkdown = () => {
      const md = [
        `### ⚖️ Mesh Part Comparison: Part #${compData.Part_A.Index} vs Part #${compData.Part_B.Index}`,
        `**Creature**: ${compData.Creature} (WCID ${compData.WCID}) | **Palette**: ${compData.PaletteID}`,
        ``,
        `| Property | Part A (#${compData.Part_A.Index}) | Part B (#${compData.Part_B.Index}) |`,
        `| :--- | :--- | :--- |`,
        `| **Mesh Name** | ${compData.Part_A.Mesh} | ${compData.Part_B.Mesh} |`,
        `| **Material Name** | ${compData.Part_A.Material} | ${compData.Part_B.Material} |`,
        `| **Texture DID** | \`${compData.Part_A.Texture_DID}\` | \`${compData.Part_B.Texture_DID}\` |`,
        `| **Blending Mode** | \`${compData.Part_A.Blending}\` | \`${compData.Part_B.Blending}\` |`,
        `| **Transparent** | ${compData.Part_A.Transparent} | ${compData.Part_B.Transparent} |`,
        `| **Visibility** | ${compData.Part_A.Visibility} | ${compData.Part_B.Visibility} |`
      ].join('\n');

      navigator.clipboard.writeText(md);
      setCopiedCompareText("Copied Markdown!");
      setTimeout(() => setCopiedCompareText(null), 2500);
    };

    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-md p-4">
        <div className="bg-[#111827] border border-[#374151] rounded-2xl p-6 w-full max-w-2xl shadow-2xl flex flex-col gap-4 text-sm text-neutral-200">
          <div className="flex justify-between items-center border-b border-[#374151] pb-3">
            <h2 className="text-lg font-bold text-white flex items-center gap-2">
              ⚖️ Side-by-Side Mesh Part Inspector
            </h2>
            <button onClick={() => setIsCompareModalOpen(false)} className="text-neutral-400 hover:text-white text-xl font-bold px-2">✕</button>
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1">
              <label className="text-xs font-bold uppercase tracking-wider text-blue-400">Select Part A:</label>
              <select
                className="bg-[#1f2937] border border-[#374151] text-white p-2 rounded-lg font-mono text-xs focus:outline-none focus:border-blue-500"
                value={comparePartAIdx}
                onChange={(e) => setComparePartAIdx(Number(e.target.value))}
              >
                {meshParts.map((p, i) => (
                  <option key={i} value={i}>Part #{p.partIndex}: {p.meshName || 'Mesh'}</option>
                ))}
              </select>
            </div>

            <div className="flex flex-col gap-1">
              <label className="text-xs font-bold uppercase tracking-wider text-purple-400">Select Part B:</label>
              <select
                className="bg-[#1f2937] border border-[#374151] text-white p-2 rounded-lg font-mono text-xs focus:outline-none focus:border-purple-500"
                value={comparePartBIdx}
                onChange={(e) => setComparePartBIdx(Number(e.target.value))}
              >
                {meshParts.map((p, i) => (
                  <option key={i} value={i}>Part #{p.partIndex}: {p.meshName || 'Mesh'}</option>
                ))}
              </select>
            </div>
          </div>

          <div className="overflow-x-auto border border-[#374151] rounded-xl bg-[#0b0f19]">
            <table className="w-full text-left border-collapse text-xs font-mono">
              <thead>
                <tr className="bg-[#1f2937] border-b border-[#374151] text-neutral-300">
                  <th className="p-2.5">Property</th>
                  <th className="p-2.5 text-blue-400">Part A (# {compData.Part_A.Index})</th>
                  <th className="p-2.5 text-purple-400">Part B (# {compData.Part_B.Index})</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#1f2937]">
                <tr>
                  <td className="p-2.5 font-bold text-neutral-400">Mesh Name</td>
                  <td className="p-2.5 text-white">{compData.Part_A.Mesh}</td>
                  <td className="p-2.5 text-white">{compData.Part_B.Mesh}</td>
                </tr>
                <tr>
                  <td className="p-2.5 font-bold text-neutral-400">Texture DID</td>
                  <td className="p-2.5 text-amber-300 font-bold">{compData.Part_A.Texture_DID}</td>
                  <td className="p-2.5 text-amber-300 font-bold">{compData.Part_B.Texture_DID}</td>
                </tr>
                <tr>
                  <td className="p-2.5 font-bold text-neutral-400">Blending Mode</td>
                  <td className="p-2.5 text-emerald-300">{compData.Part_A.Blending}</td>
                  <td className="p-2.5 text-emerald-300">{compData.Part_B.Blending}</td>
                </tr>
                <tr>
                  <td className="p-2.5 font-bold text-neutral-400">Transparent</td>
                  <td className="p-2.5">{compData.Part_A.Transparent}</td>
                  <td className="p-2.5">{compData.Part_B.Transparent}</td>
                </tr>
                <tr>
                  <td className="p-2.5 font-bold text-neutral-400">Visibility</td>
                  <td className="p-2.5">{compData.Part_A.Visibility}</td>
                  <td className="p-2.5">{compData.Part_B.Visibility}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div className="flex justify-between items-center pt-2">
            <div className="text-xs text-emerald-400 font-bold font-mono">
              {copiedCompareText || ''}
            </div>
            <div className="flex gap-3">
              <button
                onClick={handleCopyJson}
                className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white font-bold rounded-lg transition-colors text-xs flex items-center gap-1.5 shadow"
              >
                📋 Copy JSON
              </button>
              <button
                onClick={handleCopyMarkdown}
                className="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white font-bold rounded-lg transition-colors text-xs flex items-center gap-1.5 shadow"
              >
                📋 Copy Markdown Table
              </button>
            </div>
          </div>
        </div>
      </div>
    );
  };

  if (compactMode) {
    return (
      <div className="w-full h-full relative bg-[#090d16] flex items-center justify-center overflow-hidden rounded-xl border border-neutral-800">
        <Canvas
          camera={{ position: [0, 1.2, 2.5], fov: 45 }}
          gl={{ preserveDrawingBuffer: true, antialias: true }}
          onCreated={({ gl }) => { glRef.current = gl; }}
          className="w-full h-full cursor-grab active:cursor-grabbing"
        >
          <ambientLight intensity={lightIntensity * 0.9} />
          <directionalLight position={[5, 10, 7]} intensity={lightIntensity * 1.2} castShadow />
          <directionalLight position={[-5, 5, -5]} intensity={lightIntensity * 0.5} />
          <pointLight position={[0, -2, 2]} intensity={0.4} color="#60a5fa" />
          
          <Suspense fallback={<CanvasLoader />}>
            <Model 
              wcid={wcid} 
              paletteId={paletteId}
              paletteSlot={paletteSlot}
              hueShift={hueShift}
              rotationSpeed={rotationSpeed}
              isRotating={isRotating}
              wireframe={wireframe}
              activeTexReplaceInfo={activeTexReplaceIdx >= 0 ? textureReplacements[activeTexReplaceIdx] : null}
              creatureParticles={particleEffectsEnabled ? creatureParticles : []}
              particleEffectsEnabled={particleEffectsEnabled}
              particleOffsets={{
                chestOrb: [chestOrbX, chestOrbY, chestOrbZ],
                fireTail: [fireTailX, fireTailY, fireTailZ],
                wings: [wingsX, wingsY, wingsZ],
                wingsCenterX,
                showHeadAura
              }}
              onCreated={({ gl }) => { glRef.current = gl; }}
              onMeshListLoaded={setMeshParts}
            />
          </Suspense>
          
          <OrbitControls 
            enablePan={true}
            enableZoom={true}
            enableRotate={true}
            autoRotate={isRotating}
            autoRotateSpeed={rotationSpeed * 2.0}
            maxPolarAngle={Math.PI / 2 + 0.1}
            minDistance={0.5}
            maxDistance={10.0}
          />
        </Canvas>
      </div>
    );
  }

  return (
    <div className="w-full h-full flex flex-row bg-[#0b0f19] text-[#e2e8f0] font-sans overflow-hidden">
      {renderCompareModal()}
      
      {/* Sidebar Controls */}
      <div className="w-[360px] min-w-[360px] max-w-[360px] h-full flex-shrink-0 bg-[#111827] border-r border-[#1f2937] p-4 flex flex-col gap-4 overflow-y-auto font-sans z-10 shadow-2xl">
        
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <div className="p-2 bg-blue-600/20 text-blue-400 rounded-lg border border-blue-500/30">
              <Globe className="w-5 h-5 animate-pulse" />
            </div>
            <div>
              <h1 className="text-lg font-bold tracking-tight text-white flex items-center gap-1.5">
                3D Showroom
                <span className="text-[10px] bg-blue-600/30 text-blue-300 px-1.5 py-0.5 rounded font-mono font-normal">v3.0</span>
              </h1>
              <p className="text-[11px] text-neutral-400">Content Developer Suite</p>
            </div>
          </div>

          <button
            onClick={handleResetToDefault}
            className="p-1.5 bg-[#1f2937] hover:bg-[#374151] text-amber-400 rounded-lg transition-colors border border-[#374151]"
            title="Reset model overrides to native DAT defaults"
          >
            <RotateCcw className="w-4 h-4" />
          </button>
        </div>

        {/* Unified Monster Search Bar (Name OR WCID) */}
        <form onSubmit={handleSearchSubmit} className="relative flex flex-col gap-1">
          <div className="relative">
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setSearchInput(e.target.value);
              }}
              placeholder="Search by Monster Name or WCID..."
              className="w-full pl-9 pr-8 py-2 bg-[#1f2937] border border-[#374151] rounded-lg text-xs text-white placeholder-neutral-400 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 font-sans shadow-inner"
            />
            <Search className="w-4 h-4 text-neutral-400 absolute left-2.5 top-2.5" />
            {searchQuery && (
              <button 
                type="button"
                onClick={() => { setSearchQuery(''); setSearchInput(''); setSearchResults([]); }}
                className="absolute right-2.5 top-2.5 text-neutral-400 hover:text-white"
              >
                <XCircle className="w-4 h-4" />
              </button>
            )}
          </div>

          {/* Dynamic Autocomplete Suggestion Dropdown */}
          {searchQuery && (
            <div className="absolute top-10 left-0 right-0 bg-[#111827] border border-[#374151] rounded-lg shadow-2xl z-30 max-h-56 overflow-y-auto flex flex-col divide-y divide-[#1f2937]">
              {isSearching ? (
                <div className="p-3 text-center text-xs text-neutral-400 italic">Searching Database...</div>
              ) : searchResults.length === 0 ? (
                <div className="p-3 text-center text-xs text-neutral-400">No monsters found for "{searchQuery}"</div>
              ) : (
                searchResults.map(item => (
                  <button
                    key={item.wcid}
                    onClick={() => {
                      setWcid(item.wcid);
                      setSearchInput(item.wcid.toString());
                      setSearchQuery('');
                      setSearchResults([]);
                    }}
                    className="p-2.5 text-left hover:bg-blue-600/20 flex items-center justify-between group transition-colors"
                  >
                    <div>
                      <div className="text-xs font-bold text-white group-hover:text-blue-300">{item.name}</div>
                      <div className="text-[10px] text-neutral-400 font-mono">WCID #{item.wcid}</div>
                    </div>
                    <ChevronRight className="w-3.5 h-3.5 text-neutral-500 group-hover:text-blue-400" />
                  </button>
                ))
              )}
            </div>
          )}

          {/* Quick Species Select Dropdown */}
          <div className="relative">
            <select
              value={wcid}
              onChange={(e) => selectPreset(Number(e.target.value))}
              className="w-full px-3 py-2 bg-[#1f2937] border border-[#374151] rounded-lg text-xs font-semibold text-neutral-200 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 shadow-inner cursor-pointer"
            >
              <option value="" disabled>-- Select Species Preset ({(speciesPresets.length || PRESET_CREATURES.length)} Unique Species) --</option>
              {(speciesPresets.length > 0 ? speciesPresets : PRESET_CREATURES).map(c => (
                <option key={c.wcid} value={c.wcid}>
                  {c.species || c.name} (WCID {c.wcid})
                </option>
              ))}
            </select>
          </div>
        </form>

        {/* Sleek Sidebar Navigation Tabs */}
        <div className="grid grid-cols-3 gap-1 p-1 bg-[#1f2937]/50 rounded-lg border border-[#374151]/50 text-xs font-semibold">
          <button
            onClick={() => setSidebarTab('palettes')}
            className={`py-1.5 rounded-md flex items-center justify-center gap-1.5 transition-all ${
              sidebarTab === 'palettes'
                ? 'bg-blue-600 text-white shadow'
                : 'text-neutral-400 hover:text-white hover:bg-[#1f2937]'
            }`}
          >
            <PaletteIcon className="w-3.5 h-3.5" /> Palettes
          </button>

          <button
            onClick={() => setSidebarTab('textures')}
            className={`py-1.5 rounded-md flex items-center justify-center gap-1.5 transition-all ${
              sidebarTab === 'textures'
                ? 'bg-blue-600 text-white shadow'
                : 'text-neutral-400 hover:text-white hover:bg-[#1f2937]'
            }`}
          >
            <Sliders className="w-3.5 h-3.5" /> Textures
          </button>

          <button
            onClick={() => setSidebarTab('catalog')}
            className={`py-1.5 rounded-md flex items-center justify-center gap-1.5 transition-all ${
              sidebarTab === 'catalog'
                ? 'bg-purple-600 text-white shadow'
                : 'text-neutral-400 hover:text-white hover:bg-[#1f2937]'
            }`}
          >
            <Sparkles className="w-3.5 h-3.5" /> Catalog ({savedCatalog.length})
          </button>
        </div>

        {/* TAB 1: SPECIES & PALETTES */}
        {sidebarTab === 'palettes' && (
          <div className="flex flex-col gap-3">
            {/* Palette Selector */}
            <div className="flex flex-col gap-1.5">
              <label className="text-xs font-semibold uppercase tracking-wider text-neutral-400 flex items-center gap-1.5">
                <PaletteIcon className="w-4 h-4 text-neutral-400" />
                Species Variant Palette
              </label>
              <select
                value={speciesPalettes.some(p => p.templateId === paletteId || p.paletteId === paletteId) ? (speciesPalettes.find(p => p.templateId === paletteId || p.paletteId === paletteId)?.templateId ?? paletteId) : paletteId}
                onChange={(e) => setPaletteId(parseInt(e.target.value))}
                className="w-full px-3 py-2 bg-[#1f2937] border border-[#374151] rounded-lg text-sm text-white focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500"
              >
                <option value={0}>Default Variant</option>
                {speciesPalettes.map(pal => (
                  <option key={pal.templateId} value={pal.templateId}>
                    {pal.name} (0x{pal.paletteId.toString(16).toUpperCase()})
                  </option>
                ))}
                {(paletteId & 0xFF000000) === 0x04000000 && !speciesPalettes.some(p => p.paletteId === paletteId) && (
                  <option value={paletteId}>
                    Custom Palette (0x{paletteId.toString(16).toUpperCase()})
                  </option>
                )}
              </select>

              {/* Active Species Variant Metadata & Swatches Card */}
              {(() => {
                const activeVariant = speciesPalettes.find(p => p.templateId === paletteId || p.paletteId === paletteId);
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

                    {/* Content Developer Export Bar */}
                    <button
                      onClick={() => {
                        const palHex = `0x${(paletteId || 0).toString(16).toUpperCase().padStart(8, '0')}`;
                        const palTemplate = activeVariant.templateId;
                        
                        const spawnCmd = `@create ${wcid} 1 ${palHex !== '0x00000000' ? palHex : palTemplate}`;
                        const propCmd = palHex !== '0x00000000' 
                          ? `@setproperty PropertyDataId.PaletteBase ${palHex}`
                          : `@setproperty PropertyInt.PaletteTemplate ${palTemplate}`;

                        const sqlSnippet = `-- Permanent Server Spawn SQL for WCID ${wcid}
DELETE FROM \`weenie_properties_d_i_d\` WHERE \`object_Id\` = ${wcid} AND \`type\` = 15;
INSERT INTO \`weenie_properties_d_i_d\` (\`object_Id\`, \`type\`, \`value\`) VALUES (${wcid}, 15, ${palHex});

DELETE FROM \`weenie_properties_int\` WHERE \`object_Id\` = ${wcid} AND \`type\` = 40;
INSERT INTO \`weenie_properties_int\` (\`object_Id\`, \`type\`, \`value\`) VALUES (${wcid}, 40, ${palTemplate});`;

                        const fullExport = `-- In-Game Admin Command (Instant Creation):\n${spawnCmd}\n\n-- In-Game Live Target Command:\n${propCmd}\n\n${sqlSnippet}`;
                        navigator.clipboard.writeText(fullExport);
                        alert(`Copied Content Developer Commands & SQL to Clipboard!\n\n${spawnCmd}\n${propCmd}`);
                      }}
                      className="w-full py-1.5 px-3 bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 border border-emerald-500/40 rounded-lg text-xs font-bold flex items-center justify-center gap-1.5 shadow transition-colors"
                    >
                      📋 Copy Developer Commands & SQL
                    </button>
                  </div>
                );
              })()}

              {/* Rate & Approve Variant Card */}
              <div className="mt-2 p-2.5 bg-[#1f2937]/40 rounded-lg border border-[#374151] flex flex-col gap-2">
                <span className="text-[10px] font-bold text-amber-300 uppercase tracking-wide flex items-center gap-1">
                  ⭐ Variant Approval Rating (Cool vs Ugly)
                </span>
                <input
                  type="text"
                  placeholder="Optional note / tag (e.g. Red Flame Gromnie)..."
                  value={ratingNote}
                  onChange={(e) => setRatingNote(e.target.value)}
                  className="w-full px-2 py-1 bg-[#111827] border border-[#374151] rounded text-[11px] text-white placeholder-neutral-500 focus:outline-none focus:border-blue-500"
                />
                <div className="flex gap-2">
                  <button
                    onClick={() => handleRateVariant('approved')}
                    className="flex-1 py-1.5 bg-emerald-600/30 hover:bg-emerald-600/50 text-emerald-300 border border-emerald-500/40 rounded text-xs font-bold flex items-center justify-center gap-1 transition-colors shadow"
                  >
                    <ThumbsUp className="w-3.5 h-3.5" /> Cool (Approve)
                  </button>
                  <button
                    onClick={() => handleRateVariant('denied')}
                    className="flex-1 py-1.5 bg-rose-600/30 hover:bg-rose-600/50 text-rose-300 border border-rose-500/40 rounded text-xs font-bold flex items-center justify-center gap-1 transition-colors shadow"
                  >
                    <ThumbsDown className="w-3.5 h-3.5" /> Ugly (Deny)
                  </button>
                </div>
              </div>

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

              <button
                onClick={randomizePalette}
                className="mt-2 w-full flex items-center justify-center gap-2 py-2 bg-[#1f2937] hover:bg-[#374151] text-white font-semibold rounded-lg text-sm transition-colors border border-[#374151]"
              >
                🎲 Randomize Palette
              </button>
            </div>
          </div>
        )}

        {/* TAB 2: TEXTURES */}
        {sidebarTab === 'textures' && (
          <div className="flex flex-col gap-3">
            {/* Universal Programmatic Surface Texture Swapper (On-The-Fly) */}
            <div className="flex flex-col gap-2 bg-[#1f2937]/30 p-3 rounded-lg border border-[#374151]/60">
              <label className="text-xs font-semibold uppercase tracking-wider text-blue-400 flex items-center gap-1.5 justify-between">
                <span className="flex items-center gap-1.5">
                  <Sliders className="w-4 h-4 text-blue-400" />
                  Surface Texture Swapper
                </span>
                <button
                  onClick={() => {
                    if (textureLibrary.length === 0) return;
                    const randomTex = textureLibrary[Math.floor(Math.random() * textureLibrary.length)];
                    if (randomTex) {
                      setSelectedLibTexId(randomTex.textureId);
                      setCustomTexHex('');
                      appendLog('user', `Randomized texture -> ${randomTex.name} (${randomTex.hexId})`);
                    }
                  }}
                  className="px-2 py-0.5 bg-purple-600/30 hover:bg-purple-600/50 text-purple-300 border border-purple-500/40 rounded text-[10px] font-bold transition-colors flex items-center gap-1"
                  title="Randomly pick a texture from the DAT Texture Library"
                >
                  🎲 Random
                </button>
              </label>

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

              <div className="flex gap-2 mt-2">
                <button
                  onClick={applyUniversalTextureSwap}
                  disabled={!targetSurfaceId || (!selectedLibTexId && !customTexHex)}
                  className="flex-1 flex items-center justify-center gap-1.5 py-2 bg-blue-600 hover:bg-blue-500 disabled:bg-neutral-800 disabled:text-neutral-500 text-white font-semibold rounded-lg text-xs transition-colors shadow-md"
                >
                  ⚡ Apply Texture Swap Live
                </button>
              </div>

              {/* Similar Textures Explorer Panel */}
              {similarTextures && similarTextures.length > 0 && (
                <div className="mt-3 p-2 bg-[#111827] rounded-lg border border-[#374151] flex flex-col gap-1.5">
                  <span className="text-[10px] font-semibold text-purple-300 uppercase tracking-wide flex items-center justify-between">
                    <span>🎨 Similar Textures Explorer</span>
                    <span className="text-[9px] text-neutral-400 font-normal">{similarTextures.length} matching patterns</span>
                  </span>
                  <div className="grid grid-cols-2 gap-1.5">
                    {similarTextures.map(tex => (
                      <button
                        key={tex.textureId}
                        onClick={() => {
                          setSelectedLibTexId(tex.textureId);
                          setCustomTexHex('');
                        }}
                        className="p-1.5 rounded text-left border border-[#374151] bg-[#1f2937]/50 text-neutral-300 text-[10px] font-mono hover:bg-[#1f2937]"
                      >
                        <span className="font-bold block truncate">{tex.name}</span>
                        <span className="text-[9px] text-neutral-400">{tex.hexId}</span>
                      </button>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* TAB 3: SAVED CATALOG & REVIEWS */}
        {sidebarTab === 'catalog' && (
          <div className="flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-white uppercase tracking-wider">Saved Catalog</span>
              <div className="flex gap-1 text-[10px]">
                <button 
                  onClick={() => setSavedFilter('all')} 
                  className={`px-2 py-0.5 rounded font-bold transition-colors ${savedFilter === 'all' ? 'bg-blue-600 text-white' : 'bg-[#1f2937] text-neutral-400'}`}
                >All</button>
                <button 
                  onClick={() => setSavedFilter('approved')} 
                  className={`px-2 py-0.5 rounded font-bold transition-colors ${savedFilter === 'approved' ? 'bg-emerald-600 text-white' : 'bg-[#1f2937] text-neutral-400'}`}
                >Cool 👍</button>
                <button 
                  onClick={() => setSavedFilter('denied')} 
                  className={`px-2 py-0.5 rounded font-bold transition-colors ${savedFilter === 'denied' ? 'bg-rose-600 text-white' : 'bg-[#1f2937] text-neutral-400'}`}
                >Ugly 👎</button>
              </div>
            </div>

            {savedCatalog.filter(item => savedFilter === 'all' || item.rating === savedFilter).length === 0 ? (
              <div className="p-4 text-center text-neutral-500 text-xs italic bg-[#1f2937]/30 rounded-lg border border-[#374151]/50">
                No saved variants in catalog yet. Rate variants in the Palettes tab using 👍 Cool or 👎 Ugly to save them here!
              </div>
            ) : (
              <div className="flex flex-col gap-2 max-h-[500px] overflow-y-auto pr-1">
                {savedCatalog
                  .filter(item => savedFilter === 'all' || item.rating === savedFilter)
                  .map(item => (
                    <div 
                      key={item.id} 
                      className={`p-2.5 rounded-lg border flex flex-col gap-1.5 transition-all ${
                        item.rating === 'approved' 
                          ? 'bg-emerald-950/20 border-emerald-500/40 text-emerald-100' 
                          : 'bg-rose-950/20 border-rose-500/40 text-rose-100'
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1.5">
                          {item.rating === 'approved' ? (
                            <span className="px-1.5 py-0.5 bg-emerald-500/20 text-emerald-300 font-bold text-[10px] rounded border border-emerald-500/40 flex items-center gap-1">
                              <ThumbsUp className="w-3 h-3" /> COOL
                            </span>
                          ) : (
                            <span className="px-1.5 py-0.5 bg-rose-500/20 text-rose-300 font-bold text-[10px] rounded border border-rose-500/40 flex items-center gap-1">
                              <ThumbsDown className="w-3 h-3" /> UGLY
                            </span>
                          )}
                          <span className="text-xs font-bold text-white">{item.creatureName}</span>
                        </div>

                        <button
                          onClick={() => handleRemoveFromCatalog(item.id)}
                          className="text-neutral-400 hover:text-rose-400 p-0.5 transition-colors"
                          title="Remove from catalog"
                        >
                          <XCircle className="w-3.5 h-3.5" />
                        </button>
                      </div>

                      <div className="text-[11px] font-semibold text-neutral-200">
                        {item.note}
                      </div>

                      <div className="text-[10px] font-mono text-neutral-400 flex justify-between">
                        <span>WCID #{item.wcid}</span>
                        <span>Palette: {item.paletteHex}</span>
                      </div>

                      {/* Swatches Bar */}
                      {item.swatches && item.swatches.length > 0 && (
                        <div className="flex gap-1 h-3 rounded overflow-hidden border border-[#374151]">
                          {item.swatches.map((hex, idx) => (
                            <div key={idx} className="flex-1 h-full" style={{ backgroundColor: hex }} />
                          ))}
                        </div>
                      )}

                      {/* Action Bar */}
                      <button
                        onClick={() => {
                          setWcid(item.wcid);
                          setPaletteId(item.paletteId);
                        }}
                        className="mt-1 w-full py-1 bg-blue-600/40 hover:bg-blue-600 text-blue-200 font-semibold rounded text-xs transition-colors flex items-center justify-center gap-1"
                      >
                        ⚡ Reload in 3D Viewport
                      </button>
                    </div>
                  ))}
              </div>
            )}
          </div>
        )}

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

          <div className="flex justify-between items-center gap-1.5 pb-1">
            <div className="flex gap-1 overflow-x-auto pb-1 scrollbar-thin scrollbar-thumb-neutral-600 scrollbar-track-transparent">
              {['All', 'Chitin', 'Fur/Hide', 'Metallic', 'Elemental'].map(family => (
                <button
                  key={family}
                  onClick={() => setSmartFamily(family === 'All' ? 'all' : family)}
                  className={`px-2.5 py-0.5 text-[10px] font-semibold rounded-full whitespace-nowrap transition-colors ${
                    (smartFamily === 'all' && family === 'All') || smartFamily === family
                      ? 'bg-blue-600 text-white' 
                      : 'bg-[#1f2937] text-neutral-400 hover:text-neutral-200'
                  }`}
                >
                  {family}
                </button>
              ))}
            </div>
            <select
              value={minConfidenceScore}
              onChange={(e) => setMinConfidenceScore(parseInt(e.target.value))}
              className="bg-[#111827] text-amber-300 border border-amber-500/40 rounded px-2 py-0.5 text-[10px] font-bold focus:outline-none cursor-pointer"
            >
              <option value={85}>🔥 S-Tier (85%+)</option>
              <option value={70}>✨ A-Tier (70%+)</option>
              <option value={50}>👍 B-Tier (50%+)</option>
              <option value={0}>🌐 All</option>
            </select>
          </div>

          {/* Speed Curation Queue Status Banner & Tabs */}
          <div className="flex flex-col gap-1 text-[11px] font-semibold bg-[#111827] p-2 rounded-lg border border-[#374151] mb-1">
            <div className="flex justify-between items-center text-neutral-300">
              <span className="flex items-center gap-1 font-bold text-amber-400">
                🎯 Speed Curation Queue
              </span>
              <span className="text-[10px] text-neutral-400">
                👍 {approvedCount} Approved | 👎 {blacklistedCount} Blacklisted
              </span>
            </div>

            <div className="grid grid-cols-4 gap-1 mt-1">
              <button
                onClick={() => setCurationQueueTab('unrated')}
                className={`py-1 text-[10px] rounded font-bold transition-all ${
                  curationQueueTab === 'unrated' ? 'bg-amber-500 text-black shadow' : 'bg-[#1f2937] text-neutral-400 hover:text-white'
                }`}
              >
                📥 Unrated ({unratedCount})
              </button>
              <button
                onClick={() => setCurationQueueTab('approved')}
                className={`py-1 text-[10px] rounded font-bold transition-all ${
                  curationQueueTab === 'approved' ? 'bg-green-600 text-white shadow' : 'bg-[#1f2937] text-neutral-400 hover:text-white'
                }`}
              >
                👍 Approved ({approvedCount})
              </button>
              <button
                onClick={() => setCurationQueueTab('blacklisted')}
                className={`py-1 text-[10px] rounded font-bold transition-all ${
                  curationQueueTab === 'blacklisted' ? 'bg-red-600 text-white shadow' : 'bg-[#1f2937] text-neutral-400 hover:text-white'
                }`}
              >
                👎 Blacklisted ({blacklistedCount})
              </button>
              <button
                onClick={() => setCurationQueueTab('all')}
                className={`py-1 text-[10px] rounded font-bold transition-all ${
                  curationQueueTab === 'all' ? 'bg-blue-600 text-white shadow' : 'bg-[#1f2937] text-neutral-400 hover:text-white'
                }`}
              >
                🌐 All
              </button>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2 max-h-48 overflow-y-auto scrollbar-thin scrollbar-thumb-neutral-600 pr-1">
            {filteredSmartPalettes.map((pal: any) => (
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
                  <span className={`text-[9px] font-bold px-1 py-0.2 rounded ${
                    (pal.confidenceScore ?? 80) >= 85 ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30' :
                    (pal.confidenceScore ?? 80) >= 70 ? 'bg-blue-500/20 text-blue-300 border border-blue-500/30' :
                    'bg-neutral-700 text-neutral-400'
                  }`}>
                    {(pal.confidenceScore ?? 80) >= 85 ? '🔥' : '✨'} {pal.confidenceScore ?? 80}%
                  </span>
                </div>
                <div className="flex w-full h-3 rounded overflow-hidden">
                  {pal.swatches.map((hex: string, i: number) => (
                    <div key={i} className="flex-1 h-full" style={{ backgroundColor: hex }} />
                  ))}
                </div>
              </button>
            ))}
            {filteredSmartPalettes.length === 0 && (
              <div className="col-span-2 text-center text-xs text-neutral-500 py-4">
                {curationQueueTab === 'unrated' 
                  ? '🎉 All Palettes Curated! Unrated queue empty for this filter.'
                  : 'No palettes found in this view category.'}
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
              <Sparkles className="w-4 h-4 text-amber-400" /> Particle Effects
            </span>
            <input
              type="checkbox"
              checked={particleEffectsEnabled}
              onChange={() => setParticleEffectsEnabled(!particleEffectsEnabled)}
              className="w-4 h-4 rounded border-[#374151] bg-[#1f2937] text-amber-500 focus:ring-amber-500"
            />
          </div>

          {particleEffectsEnabled && (
            <div className="flex flex-col gap-2.5 p-3 bg-[#111827] border border-amber-500/40 rounded-lg text-xs shadow-lg">
              <span className="font-bold text-amber-400 flex items-center justify-between border-b border-[#374151]/50 pb-1.5">
                <span className="flex items-center gap-1.5">
                  <Sliders className="w-3.5 h-3.5 text-amber-400" /> Live 3D Particle Positioner
                </span>
                <button
                  type="button"
                  onClick={() => {
                    const text = `chestOrb: [${chestOrbX}, ${chestOrbY}, ${chestOrbZ}], fireTail: [${fireTailX}, ${fireTailY}, ${fireTailZ}], wings: [${wingsX}, ${wingsY}, ${wingsZ}], showHeadAura: ${showHeadAura}`;
                    navigator.clipboard.writeText(text);
                    alert(`Copied 3D particle offset coordinates:\n${text}`);
                  }}
                  className="px-2 py-0.5 bg-amber-600/30 hover:bg-amber-600/50 text-amber-200 border border-amber-500/40 rounded text-[10px] font-bold transition-all"
                >
                  📋 Copy 3D Offsets
                </button>
              </span>

              {/* Category Sub-Tabs */}
              <div className="flex gap-1 bg-[#1f2937]/70 p-1 rounded-md border border-[#374151]/50">
                <button
                  type="button"
                  onClick={() => setActiveParticleTab('chest')}
                  className={`flex-1 py-1 text-[10px] font-bold rounded transition-all ${
                    activeParticleTab === 'chest'
                      ? 'bg-amber-600 text-white shadow'
                      : 'text-neutral-400 hover:text-white'
                  }`}
                >
                  🔴 Chest Orb
                </button>
                <button
                  type="button"
                  onClick={() => setActiveParticleTab('fire')}
                  className={`flex-1 py-1 text-[10px] font-bold rounded transition-all ${
                    activeParticleTab === 'fire'
                      ? 'bg-orange-600 text-white shadow'
                      : 'text-neutral-400 hover:text-white'
                  }`}
                >
                  🔥 Fire Tail
                </button>
                <button
                  type="button"
                  onClick={() => setActiveParticleTab('wings')}
                  className={`flex-1 py-1 text-[10px] font-bold rounded transition-all ${
                    activeParticleTab === 'wings'
                      ? 'bg-purple-600 text-white shadow'
                      : 'text-neutral-400 hover:text-white'
                  }`}
                >
                  🪽 Wings
                </button>
              </div>

              {/* 🔴 CHEST ORB SLIDERS */}
              {activeParticleTab === 'chest' && (
                <div className="flex flex-col gap-2 pt-1">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-amber-300 font-bold mb-1">
                    <input
                      type="checkbox"
                      checked={showChestOrb}
                      onChange={(e) => setShowChestOrb(e.target.checked)}
                      className="rounded border-neutral-700 text-amber-600 focus:ring-amber-500"
                    />
                    <span>Show Red Chest Orb Emitter</span>
                  </label>
                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Height (Y Up/Down):</span>
                      <span className="font-mono text-amber-300 font-bold">{chestOrbY.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-0.50"
                      max="2.50"
                      step="0.01"
                      value={chestOrbY}
                      onChange={(e) => setChestOrbY(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-amber-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Side-to-Side (X Left/Right):</span>
                      <span className="font-mono text-amber-300 font-bold">{chestOrbX.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-1.00"
                      max="1.00"
                      step="0.01"
                      value={chestOrbX}
                      onChange={(e) => setChestOrbX(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-amber-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Depth (Z Forward/Back):</span>
                      <span className="font-mono text-amber-300 font-bold">{chestOrbZ.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-0.50"
                      max="1.00"
                      step="0.01"
                      value={chestOrbZ}
                      onChange={(e) => setChestOrbZ(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-amber-500"
                    />
                  </div>
                </div>
              )}

              {/* 🔥 BOTTOM FIRE TAIL SLIDERS */}
              {activeParticleTab === 'fire' && (
                <div className="flex flex-col gap-2 pt-1">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-orange-300 font-bold mb-0.5">
                    <input
                      type="checkbox"
                      checked={showFireTail}
                      onChange={(e) => setShowFireTail(e.target.checked)}
                      className="rounded border-neutral-700 text-orange-600 focus:ring-orange-500"
                    />
                    <span>Show Fire Tail Particles</span>
                  </label>
                  <label className="flex items-center gap-2 cursor-pointer text-[11px] text-amber-200/80 font-medium mb-1">
                    <input
                      type="checkbox"
                      checked={showTailConeMesh}
                      onChange={(e) => setShowTailConeMesh(e.target.checked)}
                      className="rounded border-neutral-700 text-amber-600 focus:ring-amber-500"
                    />
                    <span>Render Solid 3D Cone Pedestal</span>
                  </label>
                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Height (Y Up/Down):</span>
                      <span className="font-mono text-orange-400 font-bold">{fireTailY.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-0.50"
                      max="2.00"
                      step="0.01"
                      value={fireTailY}
                      onChange={(e) => setFireTailY(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-orange-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Side-to-Side (X Left/Right):</span>
                      <span className="font-mono text-orange-400 font-bold">{fireTailX.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-1.00"
                      max="1.00"
                      step="0.01"
                      value={fireTailX}
                      onChange={(e) => setFireTailX(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-orange-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Depth (Z Forward/Back):</span>
                      <span className="font-mono text-orange-400 font-bold">{fireTailZ.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-1.00"
                      max="1.00"
                      step="0.01"
                      value={fireTailZ}
                      onChange={(e) => setFireTailZ(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-orange-500"
                    />
                  </div>
                </div>
              )}

              {/* 🪽 ENERGY WINGS SLIDERS */}
              {activeParticleTab === 'wings' && (
                <div className="flex flex-col gap-2 pt-1">
                  <label className="flex items-center gap-2 cursor-pointer text-xs text-purple-300 font-bold mb-1">
                    <input
                      type="checkbox"
                      checked={showWings}
                      onChange={(e) => setShowWings(e.target.checked)}
                      className="rounded border-neutral-700 text-purple-600 focus:ring-purple-500"
                    />
                    <span>Show 4 Crescent Energy Wings</span>
                  </label>
                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Wings Height (Y):</span>
                      <span className="font-mono text-purple-300 font-bold">{wingsY.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="0.00"
                      max="2.50"
                      step="0.01"
                      value={wingsY}
                      onChange={(e) => setWingsY(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-purple-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Wings Center (X Side-to-Side):</span>
                      <span className="font-mono text-purple-300 font-bold">{wingsCenterX.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-1.00"
                      max="1.00"
                      step="0.01"
                      value={wingsCenterX}
                      onChange={(e) => setWingsCenterX(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-purple-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Wings Spread (X Width):</span>
                      <span className="font-mono text-purple-300 font-bold">{wingsX.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="0.10"
                      max="1.50"
                      step="0.01"
                      value={wingsX}
                      onChange={(e) => setWingsX(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-purple-500"
                    />
                  </div>

                  <div className="flex flex-col gap-1">
                    <div className="flex justify-between text-neutral-300">
                      <span>Wings Depth (Z):</span>
                      <span className="font-mono text-purple-300 font-bold">{wingsZ.toFixed(2)}m</span>
                    </div>
                    <input
                      type="range"
                      min="-1.00"
                      max="1.00"
                      step="0.01"
                      value={wingsZ}
                      onChange={(e) => setWingsZ(parseFloat(e.target.value))}
                      className="w-full h-1.5 bg-[#1f2937] rounded-lg appearance-none cursor-pointer accent-purple-500"
                    />
                  </div>
                </div>
              )}

              <div className="flex items-center justify-between text-neutral-300 pt-1 border-t border-[#374151]/40">
                <span>Show Pink Head Flame Dot:</span>
                <input
                  type="checkbox"
                  checked={showHeadAura}
                  onChange={() => setShowHeadAura(!showHeadAura)}
                  className="w-3.5 h-3.5 rounded border-[#374151] bg-[#1f2937] text-amber-500 focus:ring-amber-500"
                />
              </div>
            </div>
          )}

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
                  className="bg-blue-500 h-full transition-all duration-300"
                  style={{ width: `${(bulkProgress / bulkTotal) * 100}%` }}
                />
              </div>
            </div>
          ) : (
            <button
              type="button"
              onClick={startBulkExport}
              className="w-full py-2 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white font-semibold rounded-lg text-sm shadow-md transition-all flex items-center justify-center gap-2"
            >
              <Camera className="w-4 h-4" /> Start Bulk Render
            </button>
          )}
        </div>
      </div>

      {/* Main 3D Canvas Area */}
      <div className="flex-1 relative bg-[#090d16] flex items-center justify-center overflow-hidden">
        {/* Top Control Bar with Integrated Quality Control & In-Game Command Copy Buttons */}
        <div className="absolute top-4 left-4 right-4 z-20 flex flex-wrap items-center justify-between bg-[#111827]/90 backdrop-blur-md border border-[#374151] rounded-xl p-3 shadow-2xl gap-3">
          <div className="flex items-center gap-3">
            <span className="font-bold text-base text-white tracking-wide">
              {PRESET_CREATURES.find(c => c.wcid === wcid)?.name || `WCID ${wcid}`}
            </span>
            <span className="text-xs px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-300 border border-blue-500/30 font-mono">
              WCID: {wcid}
            </span>
            {creatureParticles.length > 0 && (
              <span className="text-xs px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30 font-mono flex items-center gap-1">
                <Sparkles className="w-3 h-3" /> {creatureParticles.length} Emitters
              </span>
            )}
          </div>

          {/* Quality Control Quick Actions */}
          <div className="flex items-center gap-2 bg-[#1f2937]/60 px-3 py-1.5 rounded-lg border border-[#374151]">
            <div className="flex items-center gap-1.5 text-xs font-semibold text-white mr-1">
              {currentCurationRating === 1 && <span className="text-green-400 flex items-center gap-1"><CheckCircle2 className="w-4 h-4" /> Approved</span>}
              {currentCurationRating === -1 && <span className="text-red-400 flex items-center gap-1"><XCircle className="w-4 h-4" /> Blacklisted</span>}
              {currentCurationRating === 0 && <span className="text-neutral-400 flex items-center gap-1"><AlertTriangle className="w-4 h-4 text-amber-400" /> Unrated</span>}
            </div>

            <button
              onClick={() => handleCurationSubmit(1)}
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-bold transition-all ${
                currentCurationRating === 1
                  ? 'bg-green-600 text-white ring-2 ring-green-400 shadow'
                  : 'bg-green-600/20 text-green-400 hover:bg-green-600 hover:text-white border border-green-500/40'
              }`}
              title="Approve Combination (Shortcut: A)"
            >
              <ThumbsUp className="w-3.5 h-3.5" /> Approve (A)
            </button>

            <button
              onClick={() => handleCurationSubmit(-1)}
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded text-xs font-bold transition-all ${
                currentCurationRating === -1
                  ? 'bg-red-600 text-white ring-2 ring-red-400 shadow'
                  : 'bg-red-600/20 text-red-400 hover:bg-red-600 hover:text-white border border-red-500/40'
              }`}
              title="Blacklist Combination (Shortcut: X)"
            >
              <ThumbsDown className="w-3.5 h-3.5" /> Blacklist (X)
            </button>
          </div>

          {/* In-Game Command & Debug Export Bar */}
          <div className="flex items-center gap-2">
            <button
              onClick={handleCopyCreateCmd}
              className="flex items-center gap-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs px-3 py-1.5 rounded-lg shadow-lg border border-emerald-400/30 transition-all cursor-pointer"
              title="Copy in-game @create command to clipboard"
            >
              <Copy className="w-3.5 h-3.5" />
              {copiedCreateCmd ? '✓ Copied @create!' : '⚔️ Copy @create'}
            </button>
            <button
              onClick={handleCopySetCmd}
              className="flex items-center gap-1.5 bg-purple-600 hover:bg-purple-500 text-white font-bold text-xs px-3 py-1.5 rounded-lg shadow-lg border border-purple-400/30 transition-all cursor-pointer"
              title="Copy in-game @set command to clipboard"
            >
              <Copy className="w-3.5 h-3.5" />
              {copiedSetCmd ? '✓ Copied @set!' : '🎯 Copy @set'}
            </button>
            <button
              onClick={handleCopyDebugInfo}
              className="flex items-center gap-1.5 bg-amber-600 hover:bg-amber-500 text-white font-bold text-xs px-3 py-1.5 rounded-lg shadow-lg border border-amber-400/30 transition-all cursor-pointer"
            >
              <Copy className="w-3.5 h-3.5" />
              {copiedDebug ? '✓ Copied Debug Log!' : '📋 Copy Debug Info'}
            </button>
            <button
              onClick={() => setIsRotating(!isRotating)}
              className={`p-2 rounded-lg text-sm font-semibold flex items-center gap-1.5 transition-all ${
                isRotating 
                  ? 'bg-blue-600 text-white shadow-md' 
                  : 'bg-[#1f2937] text-neutral-300 hover:bg-[#374151]'
              }`}
            >
              <RotateCw className={`w-4 h-4 ${isRotating ? 'animate-spin' : ''}`} />
              Auto Rotate
            </button>
          </div>
        </div>

        {/* 3D Canvas */}
        <div className="absolute inset-0 w-full h-full z-0">
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
                  key={`${wcid}_${paletteId}_${hueShift}_${paletteSlot}`}
                  wcid={wcid}
                  paletteId={paletteId}
                  paletteSlot={paletteSlot}
                  hueShift={hueShift}
                  activeTexReplaceInfo={activeTexReplaceIdx >= 0 ? textureReplacements[activeTexReplaceIdx] : null}
                  rotationSpeed={rotationSpeed}
                  isRotating={isRotating}
                  wireframe={wireframe}
                  creatureParticles={creatureParticles}
                  particleEffectsEnabled={particleEffectsEnabled}
                  particleOffsets={{ chestOrbX, chestOrbY, chestOrbZ, fireTailX, fireTailY, fireTailZ, wingsCenterX, wingsX, wingsY, wingsZ, showHeadAura, showChestOrb, showFireTail, showWings, showTailConeMesh }}
                  onCreated={(gl) => { glRef.current = gl; }}
                  onMeshListLoaded={setMeshParts}
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
            />
          </Canvas>
        </div>

        {/* Live Mesh Part & Material Inspector Panel Overlay */}
        {meshParts.length > 0 && (
          <div className="absolute bottom-4 left-4 right-4 bg-[#111827]/95 backdrop-blur-md border border-[#374151] rounded-xl p-3 shadow-2xl flex flex-col gap-2 max-h-64 overflow-y-auto z-20">
            <div className="flex items-center justify-between border-b border-[#374151] pb-2">
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-amber-400 uppercase tracking-wider flex items-center gap-1.5">
                  🔍 Live Mesh Part Inspector ({meshParts.length} Parts Loaded)
                </span>
                <button
                  onClick={handleShowAllParts}
                  className="text-[10px] bg-blue-600 hover:bg-blue-500 text-white font-bold px-2 py-0.5 rounded shadow"
                >
                  🌐 Show All Parts
                </button>
                <button
                  onClick={() => setIsCompareModalOpen(true)}
                  className="text-[10px] bg-purple-600 hover:bg-purple-500 text-white font-bold px-2 py-0.5 rounded shadow flex items-center gap-1"
                >
                  ⚖️ Compare Parts
                </button>
              </div>
              <button 
                onClick={() => setShowMeshInspector(!showMeshInspector)}
                className="text-xs text-neutral-300 hover:text-white px-2.5 py-1 bg-[#1f2937] hover:bg-[#374151] rounded border border-[#374151] font-semibold"
              >
                {showMeshInspector ? 'Hide Inspector Panel' : 'Show Inspector Panel'}
              </button>
            </div>

            {showMeshInspector && (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2 text-xs font-mono">
                {meshParts.map((part) => (
                  <div key={part.partIndex} className={`p-2 rounded border flex flex-col gap-1.5 transition-all ${
                    part.visible === false ? 'bg-[#0f172a]/50 border-red-500/30 opacity-60' : 'bg-[#1f2937]/80 border-[#374151]'
                  }`}>
                    <div className="flex justify-between items-center">
                      <span className="font-bold text-blue-300">Part #{part.partIndex}: {part.meshName}</span>
                      <div className="flex items-center gap-1">
                        <button
                          onClick={() => handleTogglePartVisibility(part.partIndex)}
                          className={`px-1.5 py-0.5 text-[10px] font-bold rounded border ${
                            part.visible === false ? 'bg-red-950 text-red-300 border-red-500/40' : 'bg-green-950 text-green-300 border-green-500/40'
                          }`}
                          title="Toggle Part Visibility"
                        >
                          {part.visible === false ? '🙈 Hidden' : '👁️ Visible'}
                        </button>
                        <button
                          onClick={() => handleIsolatePart(part.partIndex)}
                          className="px-1.5 py-0.5 text-[10px] font-bold rounded bg-amber-600 hover:bg-amber-500 text-black shadow"
                          title="Hide all other parts and show ONLY this part"
                        >
                          🔍 Isolate
                        </button>
                      </div>
                    </div>

                    <div className="text-[10px] text-neutral-400 truncate">
                      Mat: <span className="text-neutral-200">{part.matName}</span>
                    </div>

                    {/* Mode Controls */}
                    <div className="flex gap-1 mt-1">
                      <button
                        onClick={() => handleToggleBlending(part.partIndex, 'opaque')}
                        className={`flex-1 py-1 text-[10px] font-semibold rounded border transition-all ${
                          part.blendingMode === 'opaque'
                            ? 'bg-blue-600 text-white border-blue-400'
                            : 'bg-[#111827] text-neutral-400 border-[#374151] hover:bg-[#374151]'
                        }`}
                      >
                        Solid Opaque
                      </button>
                      <button
                        onClick={() => handleToggleBlending(part.partIndex, 'additive')}
                        className={`flex-1 py-1 text-[10px] font-semibold rounded border transition-all ${
                          part.blendingMode === 'additive'
                            ? 'bg-pink-600 text-white border-pink-400'
                            : 'bg-[#111827] text-neutral-400 border-[#374151] hover:bg-[#374151]'
                        }`}
                      >
                        Additive Glow
                      </button>
                      <button
                        onClick={() => handleToggleBlending(part.partIndex, 'blend')}
                        className={`flex-1 py-1 text-[10px] font-semibold rounded border transition-all ${
                          part.blendingMode === 'blend'
                            ? 'bg-purple-600 text-white border-purple-400'
                            : 'bg-[#111827] text-neutral-400 border-[#374151] hover:bg-[#374151]'
                        }`}
                      >
                        Alpha Blend
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}



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
