// @ts-check
import withNuxt from "./.nuxt/eslint.config.mjs";

export default withNuxt({
  name: "reel/vue-rules",
  rules: {
    "vue/no-unused-components": "warn",
    "vue/require-v-for-key": "error",
    "vue/multi-word-component-names": "off",
  },
});
