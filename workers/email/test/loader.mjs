export async function resolve(specifier, context, nextResolve) {
  if (specifier === "cloudflare:email") {
    return {
      format: "module",
      shortCircuit: true,
      url: new URL("./mock-cloudflare-email.mjs", import.meta.url).href,
    };
  }
  return nextResolve(specifier, context);
}
