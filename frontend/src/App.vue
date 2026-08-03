<script setup lang="ts">
import AuthView from './views/AuthView.vue'
import AppShell from './components/AppShell.vue'
import { session } from './session'
</script>

<template>
  <AuthView v-if="!session" />
  <AppShell v-else />
</template>
