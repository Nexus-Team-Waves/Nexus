/**
 * RFC 4122 v4 UUID generator that works in a NON-secure context too.
 *
 * WHY: `crypto.randomUUID()` is only defined in secure contexts (HTTPS or localhost). When the app
 * is served over plain HTTP on a LAN IP (e.g. http://192.168.7.27:5173 for a demo), `randomUUID`
 * is `undefined` and calling it throws — which previously blanked the Submit screen. `getRandomValues`
 * IS available in non-secure contexts, so we fall back to it (and to Math.random only as a last resort).
 */
export function uuid(): string {
  const c: Crypto | undefined = globalThis.crypto
  if (c && typeof c.randomUUID === 'function') return c.randomUUID()

  const bytes = new Uint8Array(16)
  if (c && typeof c.getRandomValues === 'function') {
    c.getRandomValues(bytes)
  } else {
    for (let i = 0; i < 16; i++) bytes[i] = Math.floor(Math.random() * 256)
  }
  // Set the version (4) and variant (10xx) bits per RFC 4122.
  bytes[6] = (bytes[6]! & 0x0f) | 0x40
  bytes[8] = (bytes[8]! & 0x3f) | 0x80

  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0'))
  return (
    `${hex[0]}${hex[1]}${hex[2]}${hex[3]}-${hex[4]}${hex[5]}-${hex[6]}${hex[7]}-` +
    `${hex[8]}${hex[9]}-${hex[10]}${hex[11]}${hex[12]}${hex[13]}${hex[14]}${hex[15]}`
  )
}
