/* Sketches of what changes from one LOD to the next, one per family of element: hosted openings
   (door), walls, slabs, structural frame, foundations, stairs, point devices (fixture), rooms,
   site surfaces, track, linear systems (duct) and equipment. Each element type maps to one family.
   Each part is drawn from one level and, if it is replaced later, until another;
   parts new at the level shown take that level's colour, earlier parts are grey.
   Drawings are OpenBEP's own. */
(function(){
  const COL={'100':'#6F8796','200':'#2E9E8F','300':'#2E6FD9','350':'#7B5CC4','400':'#D98A1C','500':'#B23A48'};
  const OLD='#9AA5A0';
  // [from, until (exclusive, '' = never replaced), svg with {c} for the stroke/fill colour]
  const SK={
    door:{cap:{
      '100':'Opening allowance or symbol only',
      '200':'Generic door, approximate size and swing',
      '300':'Designed leaf and frame, clear width measurable',
      '350':'Clearance zones, closer and wall interface',
      '400':'Frame profile and hardware preparations from shop drawings',
      '500':'Installed position checked on site to the stated tolerance'},
    parts:[
      ['100','200','<rect x="10" y="62" width="140" height="16" fill="none" stroke="{c}" stroke-width="1.5"/><circle cx="80" cy="70" r="9" fill="#fff" stroke="{c}" stroke-dasharray="3 2"/><text x="80" y="74" font-size="10" text-anchor="middle" fill="{c}" font-family="monospace">D</text>'],
      ['200','','<path d="M10 62H56V78H10ZM104 62H150V78H104Z" fill="none" stroke="{c}" stroke-width="1.5"/>'],
      ['200','300','<rect x="57" y="22" width="5" height="40" fill="{c}" opacity=".55"/><path d="M62 22A40 40 0 0 1 102 62" fill="none" stroke="{c}" stroke-dasharray="3 3"/>'],
      ['300','','<rect x="56" y="62" width="5" height="16" fill="{c}"/><rect x="99" y="62" width="5" height="16" fill="{c}"/><rect x="61" y="24" width="3" height="38" fill="{c}"/><path d="M64 24A38 38 0 0 1 99 62" fill="none" stroke="{c}"/><path d="M61 92H99M61 88V96M99 88V96" stroke="{c}"/><text x="80" y="104" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">clear width</text>'],
      ['350','','<rect x="40" y="16" width="80" height="44" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="4 3"/><rect x="40" y="80" width="80" height="0.1" stroke="none"/><rect x="64" y="30" width="8" height="4" fill="{c}"/><path d="M53 66l3 3M53 74l3-3M107 66l-3 3M107 74l-3-3" stroke="{c}" stroke-width="1.5"/>'],
      ['400','','<path d="M56 62h5v4h-2v8h2v4h-5z" fill="#fff" stroke="{c}" stroke-width="1.2"/><path d="M104 62h-5v4h2v8h-2v4h5z" fill="#fff" stroke="{c}" stroke-width="1.2"/><rect x="61" y="44" width="3" height="5" fill="{c}"/><rect x="61" y="30" width="3" height="3" fill="{c}"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="58" cy="70" r="5"/><path d="M50 70h16M58 62v16"/><circle cx="102" cy="70" r="5"/><path d="M94 70h16M102 62v16"/></g><text x="80" y="116" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">± tolerance</text>']]},
    duct:{cap:{
      '100':'Service zone reserved, no duct drawn',
      '200':'Generic duct at approximate size and height',
      '300':'Designed size, fittings and connections',
      '350':'Insulation envelope, hangers and access zones',
      '400':'Fabrication segments and joints from shop drawings',
      '500':'Installed run checked on site to the stated tolerance'},
    parts:[
      ['100','200','<rect x="8" y="34" width="144" height="36" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="5 3"/><text x="80" y="56" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">service zone</text>'],
      ['200','300','<rect x="10" y="44" width="140" height="16" fill="{c}" opacity=".5"/>'],
      ['300','','<path d="M10 44H118Q132 44 132 58V110H116V64Q116 60 112 60H10Z" fill="none" stroke="{c}" stroke-width="1.5"/><rect x="58" y="60" width="10" height="14" fill="none" stroke="{c}"/><rect x="53" y="74" width="20" height="20" fill="none" stroke="{c}"/><path d="M53 74l20 20M73 74l-20 20" stroke="{c}" stroke-width=".6"/>'],
      ['350','','<path d="M10 39H120Q137 39 137 58V110M111 110V66Q111 65 110 65H10" fill="none" stroke="{c}" stroke-dasharray="4 2"/><g stroke="{c}" stroke-width="1.4"><path d="M30 34v36M70 34v36M100 34v36"/></g><rect x="84" y="66" width="22" height="16" fill="{c}" opacity=".15" stroke="{c}" stroke-dasharray="3 2"/>'],
      ['400','','<g stroke="{c}" stroke-width="2"><path d="M45 42v20M80 42v20M110 42v20"/></g><text x="28" y="30" font-size="7" fill="{c}" font-family="monospace">L=1500</text>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="20" cy="52" r="5"/><path d="M12 52h16M20 44v16"/><circle cx="124" cy="100" r="5"/><path d="M116 100h16M124 92v16"/></g>']]},
    wall:{cap:{
      '100':'Space boundary or massing only',
      '200':'Generic wall at approximate thickness',
      '300':'Designed wall type with its layers and thickness',
      '350':'Penetrations, firestopping and movement joints',
      '400':'Studs, anchors and panel joints from shop drawings',
      '500':'Installed wall checked on site to the stated tolerance'},
    parts:[
      ['100','200','<rect x="10" y="46" width="140" height="28" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="5 3"/><text x="80" y="64" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">boundary</text>'],
      ['200','300','<rect x="10" y="52" width="140" height="16" fill="{c}" opacity=".5"/>'],
      ['300','','<rect x="10" y="50" width="140" height="20" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M10 54H150M10 66H150" stroke="{c}" stroke-width=".7"/><path d="M152 50h6M152 70h6M155 50v20" stroke="{c}"/><text x="80" y="88" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">layers + thickness</text>'],
      ['350','','<circle cx="56" cy="60" r="6" fill="#fff" stroke="{c}"/><circle cx="56" cy="60" r="9" fill="{c}" opacity=".15" stroke="{c}" stroke-dasharray="2 2"/><path d="M112 48v24M116 48v24" stroke="{c}" stroke-width="1.2"/>'],
      ['400','','<g fill="{c}"><rect x="20" y="55" width="3" height="10"/><rect x="36" y="55" width="3" height="10"/><rect x="76" y="55" width="3" height="10"/><rect x="92" y="55" width="3" height="10"/><rect x="128" y="55" width="3" height="10"/><rect x="144" y="55" width="3" height="10"/></g>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="14" cy="60" r="5"/><path d="M6 60h16M14 52v16"/><circle cx="146" cy="60" r="5"/><path d="M138 60h16M146 52v16"/></g>']]},
    slab:{cap:{
      '100':'Floor plate as a line or area only',
      '200':'Generic slab at approximate thickness',
      '300':'Designed thickness, edges, depressions and slopes',
      '350':'Sleeves, openings and edge connections',
      '400':'Reinforcement, embeds and deck profile from shop drawings',
      '500':'Installed slab checked on site to the stated tolerance'},
    parts:[
      ['100','200','<path d="M10 58H150" stroke="{c}" stroke-width="1.5" stroke-dasharray="6 3"/><text x="80" y="50" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">floor plate</text>'],
      ['200','300','<rect x="10" y="52" width="140" height="12" fill="{c}" opacity=".5"/>'],
      ['300','','<path d="M10 50H92V55H122V50H150V64H10Z" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M4 50v14M1 50h6M1 64h6" stroke="{c}"/><path d="M96 44h22l-4-3M118 44l-4 3" stroke="{c}" fill="none"/><text x="107" y="38" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">slope</text>'],
      ['350','','<rect x="38" y="49" width="8" height="16" fill="#fff" stroke="{c}" stroke-dasharray="2 2"/><path d="M150 50h6v22h-12" fill="none" stroke="{c}" stroke-width="1.4"/>'],
      ['400','','<path d="M14 54H88M14 60H146" stroke="{c}" stroke-dasharray="1 3" stroke-width="1.6"/><rect x="66" y="64" width="10" height="3" fill="{c}"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="20" cy="80" r="5"/><path d="M12 80h16M20 72v16"/><circle cx="140" cy="80" r="5"/><path d="M132 80h16M140 72v16"/></g>']]},
    frame:{cap:{
      '100':'Structural grid and nominal positions',
      '200':'Generic members at approximate size',
      '300':'Designed sections, levels and orientation',
      '350':'Connections, base plates and anchors',
      '400':'Bolts, holes and copes from shop drawings',
      '500':'Installed frame checked on site to the stated tolerance'},
    parts:[
      ['100','200','<path d="M40 8V112M120 8V112" stroke="{c}" stroke-dasharray="8 3 2 3"/><circle cx="40" cy="22" r="4" fill="{c}"/><circle cx="120" cy="22" r="4" fill="{c}"/>'],
      ['200','300','<rect x="34" y="20" width="12" height="90" fill="{c}" opacity=".5"/><rect x="46" y="20" width="80" height="10" fill="{c}" opacity=".5"/>'],
      ['300','','<rect x="34" y="16" width="12" height="94" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M36 16V110M44 16V110" stroke="{c}" stroke-width=".6"/><rect x="46" y="16" width="80" height="12" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M46 18H126M46 26H126" stroke="{c}" stroke-width=".6"/><text x="86" y="12" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">top of steel</text>'],
      ['350','','<rect x="46" y="16" width="6" height="14" fill="{c}"/><rect x="28" y="110" width="24" height="3" fill="{c}"/><path d="M32 113v6M48 113v6" stroke="{c}" stroke-width="1.5"/>'],
      ['400','','<g fill="#fff" stroke="{c}"><circle cx="49" cy="19" r="1.4"/><circle cx="49" cy="24" r="1.4"/><circle cx="49" cy="29" r="1.4"/></g><path d="M120 28v-6h6" fill="none" stroke="{c}"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="40" cy="8" r="5"/><path d="M32 8h16M40 0v16"/><circle cx="126" cy="40" r="5"/><path d="M118 40h16M126 32v16"/></g>']]},
    footing:{cap:{
      '100':'Foundation type and an allowance per support',
      '200':'Generic footing and pier at approximate size',
      '300':'Designed size with top and bottom elevations',
      '350':'Dowels, anchor bolts and connections to the frame',
      '400':'Reinforcement cages and embeds from shop drawings',
      '500':'Installed foundation checked on site to the stated tolerance'},
    parts:[
      ['100','200','<rect x="50" y="70" width="60" height="22" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="4 3"/><text x="80" y="85" font-size="9" text-anchor="middle" fill="{c}" font-family="monospace">F1</text>'],
      ['200','300','<rect x="45" y="78" width="70" height="18" fill="{c}" opacity=".5"/><rect x="70" y="40" width="20" height="38" fill="{c}" opacity=".5"/>'],
      ['300','','<rect x="40" y="78" width="80" height="20" fill="none" stroke="{c}" stroke-width="1.5"/><rect x="70" y="38" width="20" height="40" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M124 78h12M124 98h12" stroke="{c}"/><text x="140" y="81" font-size="7" fill="{c}" font-family="monospace">T</text><text x="140" y="101" font-size="7" fill="{c}" font-family="monospace">B</text>'],
      ['350','','<path d="M74 30v20M86 30v20" stroke="{c}" stroke-width="1.5"/><rect x="66" y="36" width="28" height="3" fill="{c}"/>'],
      ['400','','<path d="M44 94H116" stroke="{c}" stroke-dasharray="1 3" stroke-width="2"/><path d="M73 42V92M87 42V92" stroke="{c}" stroke-width=".8"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="40" cy="78" r="5"/><path d="M32 78h16M40 70v16"/><circle cx="120" cy="98" r="5"/><path d="M112 98h16M120 90v16"/></g>']]},
    stair:{cap:{
      '100':'Stair core or shaft as a volume',
      '200':'Generic flight at approximate rise and run',
      '300':'Designed risers, treads and landings',
      '350':'Handrails, guards and headroom zone',
      '400':'Stringers, nosings and fixings from shop drawings',
      '500':'Installed stair checked on site to the stated tolerance'},
    parts:[
      ['100','200','<rect x="16" y="12" width="128" height="98" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="5 3"/><text x="80" y="64" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">stair core</text>'],
      ['200','300','<path d="M20 106L116 30H142V40H122L28 110Z" fill="{c}" opacity=".5"/>'],
      ['300','','<path d="M20 106V96H32V86H44V76H56V66H68V56H80V46H92V36H104V26H142" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M20 106L104 40H142" fill="none" stroke="{c}" stroke-width=".7"/>'],
      ['350','','<path d="M20 82L104 16H142" fill="none" stroke="{c}" stroke-width="1.5"/><path d="M40 66v-14M80 34v-14" stroke="{c}"/><path d="M110 4H142V26H110Z" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="3 2"/>'],
      ['400','','<path d="M24 112L108 46" stroke="{c}" stroke-width="2.5"/><g fill="{c}"><circle cx="44" cy="96" r="1.6"/><circle cx="72" cy="74" r="1.6"/><circle cx="100" cy="52" r="1.6"/></g>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="20" cy="106" r="5"/><path d="M12 106h16M20 98v16"/><circle cx="142" cy="26" r="5"/><path d="M134 26h16M142 18v16"/></g>']]},
    fixture:{cap:{
      '100':'Allowance per area, not modelled as objects',
      '200':'Generic symbols at approximate locations',
      '300':'Designed objects at their position and height',
      '350':'Coverage or clearance zones and the connection',
      '400':'Mounting, supports and accessories of the approved product',
      '500':'Installed items checked on site, with make and serial number'},
    parts:[
      ['100','200','<rect x="10" y="14" width="140" height="92" fill="{c}" opacity=".08" stroke="{c}" stroke-dasharray="5 3"/><text x="80" y="64" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">per m²</text>'],
      ['200','300','<circle cx="50" cy="60" r="8" fill="{c}" opacity=".5"/><circle cx="110" cy="60" r="8" fill="{c}" opacity=".5"/>'],
      ['300','','<path d="M10 40H150M10 80H150M40 14V106M80 14V106M120 14V106" stroke="{c}" stroke-width=".4"/><rect x="42" y="42" width="36" height="16" fill="none" stroke="{c}" stroke-width="1.5"/><rect x="82" y="62" width="36" height="16" fill="none" stroke="{c}" stroke-width="1.5"/>'],
      ['350','','<circle cx="60" cy="50" r="26" fill="{c}" opacity=".1" stroke="{c}" stroke-dasharray="3 2"/><circle cx="100" cy="70" r="26" fill="{c}" opacity=".1" stroke="{c}" stroke-dasharray="3 2"/><path d="M60 42V8M100 62V8" stroke="{c}"/>'],
      ['400','','<g stroke="{c}" stroke-width="1.4"><path d="M44 44l-4-4M76 44l4-4M84 64l-4-4M116 64l4-4"/></g>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="60" cy="50" r="5"/><path d="M52 50h16M60 42v16"/><circle cx="100" cy="70" r="5"/><path d="M92 70h16M100 62v16"/></g><text x="80" y="116" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">make + serial</text>']]},
    space:{cap:{
      '100':'Programme areas as blocks',
      '200':'Rooms at approximate size and position',
      '300':'Rooms bounded by designed walls, with measured areas',
      '350':'Zones (fire, HVAC, security) drawn over the rooms',
      '400':'Same rooms; room data checked against the room data sheets',
      '500':'Areas re-measured from the as-built walls'},
    parts:[
      ['100','200','<rect x="14" y="22" width="60" height="44" fill="{c}" opacity=".25"/><rect x="80" y="40" width="66" height="56" fill="{c}" opacity=".25"/><text x="44" y="48" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">120 m²</text>'],
      ['200','300','<rect x="12" y="18" width="70" height="50" fill="none" stroke="{c}" stroke-dasharray="4 2"/><rect x="82" y="18" width="66" height="84" fill="none" stroke="{c}" stroke-dasharray="4 2"/>'],
      ['300','','<path d="M10 16H150V104H10ZM84 16V104M10 70H84" fill="none" stroke="{c}" stroke-width="2.5"/><text x="47" y="42" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">101</text><text x="47" y="52" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">42.5 m²</text><text x="117" y="58" font-size="8" text-anchor="middle" fill="{c}" font-family="monospace">102</text>'],
      ['350','','<rect x="14" y="20" width="66" height="80" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="6 2 2 2"/>'],
      ['400','','<rect x="100" y="66" width="34" height="12" fill="#fff" stroke="{c}"/><path d="M104 72h26" stroke="{c}" stroke-width=".7"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="10" cy="16" r="5"/><path d="M2 16h16M10 8v16"/><circle cx="150" cy="104" r="5"/><path d="M142 104h16M150 96v16"/></g>']]},
    site:{cap:{
      '100':'Site boundary and approximate contours',
      '200':'Approximate surface and pavement areas',
      '300':'Designed grades, breaklines and pavement layers',
      '350':'Subgrade layers and crossings with utilities',
      '400':'Layer thicknesses and curb details for construction',
      '500':'Surveyed surface and points checked to the stated tolerance'},
    parts:[
      ['100','200','<path d="M8 70Q50 56 80 64T152 58M8 84Q50 72 80 80T152 74" fill="none" stroke="{c}" stroke-dasharray="5 3"/>'],
      ['200','300','<path d="M8 66L60 58L104 62L152 54V100H8Z" fill="{c}" opacity=".35"/>'],
      ['300','','<path d="M8 66L56 58H104L152 50" fill="none" stroke="{c}" stroke-width="1.8"/><g fill="{c}"><circle cx="56" cy="58" r="2.2"/><circle cx="104" cy="58" r="2.2"/></g><path d="M56 63H104" stroke="{c}" stroke-width="5" opacity=".5"/><text x="80" y="52" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">2 %</text>'],
      ['350','','<path d="M56 70H104M56 76H104" stroke="{c}" stroke-dasharray="4 2"/><circle cx="128" cy="86" r="6" fill="#fff" stroke="{c}"/>'],
      ['400','','<path d="M52 56v12h4M108 56v12h-4" fill="none" stroke="{c}" stroke-width="1.6"/><text x="80" y="92" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">layers in mm</text>'],
      ['500','','<g fill="none" stroke="{c}"><path d="M24 60l4-7 4 7z"/><path d="M76 54l4-7 4 7z"/><path d="M128 50l4-7 4 7z"/></g>']]},
    track:{cap:{
      '100':'Track centreline on a preliminary alignment',
      '200':'Generic track at approximate position',
      '300':'Designed rails, ties, gauge and cant',
      '350':'Ballast profile and clearance envelope',
      '400':'Fastenings and rail joints for construction',
      '500':'Installed track surveyed to the stated tolerance'},
    parts:[
      ['100','200','<path d="M6 60H154" stroke="{c}" stroke-width="1.5" stroke-dasharray="10 3 2 3"/><path d="M30 55v10M80 55v10M130 55v10" stroke="{c}"/>'],
      ['200','300','<rect x="6" y="50" width="148" height="20" fill="{c}" opacity=".45"/>'],
      ['300','','<g fill="{c}" opacity=".55"><rect x="12" y="46" width="6" height="28"/><rect x="30" y="46" width="6" height="28"/><rect x="48" y="46" width="6" height="28"/><rect x="66" y="46" width="6" height="28"/><rect x="84" y="46" width="6" height="28"/><rect x="102" y="46" width="6" height="28"/><rect x="120" y="46" width="6" height="28"/><rect x="138" y="46" width="6" height="28"/></g><path d="M6 52H154M6 68H154" stroke="{c}" stroke-width="2.2"/><path d="M148 52v16" stroke="{c}"/><text x="140" y="86" font-size="7" text-anchor="middle" fill="{c}" font-family="monospace">gauge</text>'],
      ['350','','<rect x="6" y="34" width="148" height="52" fill="{c}" opacity=".1" stroke="{c}" stroke-dasharray="4 2"/>'],
      ['400','','<g fill="{c}"><rect x="13" y="50" width="4" height="4"/><rect x="13" y="66" width="4" height="4"/><rect x="67" y="50" width="4" height="4"/><rect x="67" y="66" width="4" height="4"/><rect x="121" y="50" width="4" height="4"/><rect x="121" y="66" width="4" height="4"/></g><path d="M94 48v8M94 64v8" stroke="{c}" stroke-width="2"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="24" cy="100" r="5"/><path d="M16 100h16M24 92v16"/><circle cx="136" cy="100" r="5"/><path d="M128 100h16M136 92v16"/></g>']]},
    equip:{cap:{
      '100':'Space allowance for the plant',
      '200':'Generic box at approximate size',
      '300':'Designed size with duct and pipe connections',
      '350':'Service clearance, coil pull zone and pad',
      '400':'Sections and access doors of the approved unit',
      '500':'Installed unit checked on site to the stated tolerance'},
    parts:[
      ['100','200','<rect x="18" y="24" width="124" height="70" fill="{c}" opacity=".12" stroke="{c}" stroke-dasharray="5 3"/><path d="M18 24l124 70M142 24L18 94" stroke="{c}" stroke-width=".6"/>'],
      ['200','300','<rect x="30" y="36" width="96" height="40" fill="{c}" opacity=".5"/>'],
      ['300','','<rect x="34" y="38" width="92" height="34" fill="none" stroke="{c}" stroke-width="1.5"/><rect x="40" y="26" width="18" height="12" fill="none" stroke="{c}"/><rect x="100" y="26" width="18" height="12" fill="none" stroke="{c}"/><path d="M80 72v12M88 72v12" stroke="{c}" stroke-width="2"/>'],
      ['350','','<rect x="28" y="33" width="104" height="44" fill="none" stroke="{c}" stroke-dasharray="2 2"/><rect x="34" y="72" width="92" height="26" fill="{c}" opacity=".13" stroke="{c}" stroke-dasharray="4 3"/><rect x="126" y="44" width="28" height="20" fill="{c}" opacity=".13" stroke="{c}" stroke-dasharray="4 3"/>'],
      ['400','','<path d="M56 38v34M78 38v34M104 38v34" stroke="{c}"/><rect x="60" y="70" width="14" height="3" fill="{c}"/><rect x="84" y="70" width="14" height="3" fill="{c}"/>'],
      ['500','','<g stroke="{c}" fill="none"><circle cx="34" cy="38" r="5"/><path d="M26 38h16M34 30v16"/><circle cx="126" cy="72" r="5"/><path d="M118 72h16M126 64v16"/></g>']]}
  };
  // Which element types each sketch stands for.
  const OF={door:'door',window:'door',curtain_wall:'door',
    wall_interior:'wall',wall_exterior:'wall',tunnel_lining:'wall',road_barrier:'wall',
    slab:'slab',roof:'slab',ceiling:'slab',bridge_superstructure:'slab',rail_platform:'slab',
    structural_column:'frame',beam:'frame',structural_member:'frame',bridge_substructure:'frame',bridge_bearing:'frame',ocs_support:'frame',
    footing:'footing',pile:'footing',retaining_wall:'footing',utility_structure:'footing',
    stair:'stair',ramp:'stair',railing:'stair',
    light_fixture:'fixture',plumbing_fixture:'fixture',air_terminal:'fixture',sprinkler:'fixture',fire_alarm_device:'fixture',wiring_device:'fixture',comm_security_device:'fixture',signage:'fixture',rail_signal:'fixture',
    space:'space',zone:'space',
    site_terrain:'site',paving_parking:'site',road_pavement:'site',
    rail_track:'track',rail_turnout:'track',
    duct:'duct',pipe:'duct',cable_carrier:'duct',utility_pipe:'duct',damper:'duct',valve:'duct',baggage_handling:'duct',
    ahu:'equip',pump:'equip',boiler:'equip',chiller:'equip',cooling_tower:'equip',fan:'equip',vav_box:'equip',heat_exchanger:'equip',
    water_heater:'equip',distribution_board:'equip',transformer:'equip',generator:'equip',specialty_equipment:'equip',elevator:'equip',boarding_bridge:'equip',furniture_casework:'equip'};
  function svg(kind,lod){const k=SK[kind]; if(!k) return '';
    const body=k.parts.filter(([f,u])=>+f<=+lod&&(!u||+u>+lod)).map(([f,,s])=>s.replace(/\{c\}/g,f===lod?COL[f]:OLD)).join('');
    return `<svg viewBox="0 0 160 120" role="img" aria-label="${k.cap[lod]}" class="ill"><rect width="160" height="120" fill="#fff"/>${body}</svg>`}
  window.OPENBEP_LOD_ILL={svg,cap:(kind,lod)=>(SK[kind]||{cap:{}}).cap[lod]||'',of:id=>OF[id]||'',kinds:Object.keys(SK)};
})();
