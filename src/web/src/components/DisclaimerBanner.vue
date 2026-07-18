<template>
  <v-banner
    v-if="!dismissed"
    icon="mdi-information"
    color="blue-grey-lighten-5"
    lines="two"
    class="mb-4"
  >
    <template #text>
      <span class="text-body-2">
        <strong>Planning tool only.</strong> This is not financial advice. Data from federal sources may lag 1–2 years.
        Tax estimates are approximations. Individual outcomes vary significantly based on personal circumstances.
      </span>
    </template>
    <template #actions>
      <v-btn variant="text" size="small" @click="dismiss">Got it</v-btn>
    </template>
  </v-banner>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const STORAGE_KEY = 'gradcast_disclaimer_dismissed'
const dismissed = ref(true) // Start hidden to avoid flash

onMounted(() => {
  dismissed.value = localStorage.getItem(STORAGE_KEY) === 'true'
})

function dismiss() {
  dismissed.value = true
  localStorage.setItem(STORAGE_KEY, 'true')
}
</script>
