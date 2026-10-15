// @ts-check
import withNuxt from "./.nuxt/eslint.config.mjs";

const nuxtConfig = withNuxt({
  name: "reel/vue-rules",
  rules: {
    "vue/no-unused-components": "warn",
    "vue/require-v-for-key": "error",
    "vue/multi-word-component-names": "off",
    "vue/html-self-closing": "off",
  },
});

export default nuxtConfig.append({
  ignores: [".nuxt/**", ".output/**", "dist/**", "generated/**", "eslint.config.mjs"],
});
