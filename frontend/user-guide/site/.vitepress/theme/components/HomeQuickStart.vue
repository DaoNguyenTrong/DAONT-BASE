<script setup lang="ts">
import { computed } from 'vue'
import { useData, withBase } from 'vitepress'
import type { HomeFrontmatter } from '../../home'

// Every text comes from the page's frontmatter (index.md, en/index.md), so this component holds no
// copy of its own. Thumbnails reuse the chapter screenshots: an image under src/ only reaches the
// build when something imports it, hence the glob (keys are relative to this file).
const images = import.meta.glob<string>('../../../src/*/images/**/*.jpg', {
  eager: true,
  query: '?url',
  import: 'default',
})

const { frontmatter } = useData()
const quickStart = computed(() => (frontmatter.value as HomeFrontmatter).quickStart)
const thumbnail = (image: string) => images[`../../../src${image}`]
</script>

<template>
  <section v-if="quickStart?.steps.length" class="guide-quick-start">
    <h2 class="guide-quick-start__title">{{ quickStart.title }}</h2>
    <ol class="guide-quick-start__steps">
      <li
        v-for="(step, index) in quickStart.steps"
        :key="step.link"
        class="guide-quick-start__step"
      >
        <span class="guide-quick-start__number" aria-hidden="true">{{ index + 1 }}</span>
        <a class="guide-quick-start__link" :href="withBase(step.link)">
          <img
            v-if="thumbnail(step.image)"
            class="guide-quick-start__image"
            :src="thumbnail(step.image)"
            :alt="step.title"
            loading="lazy"
          />
          <strong class="guide-quick-start__name">{{ step.title }}</strong>
          <span class="guide-quick-start__details">{{ step.details }}</span>
        </a>
      </li>
    </ol>
  </section>
</template>
