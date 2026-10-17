<script setup lang="ts">
import { useData, withBase } from 'vitepress'

const { frontmatter } = useData()
const product = __GUIDE__
</script>

<template>
  <img
    class="guide-hero-wordmark"
    :src="withBase(`/brand/${product.brand.wordmarkDark}`)"
    :alt="product.publisher"
  />
  <h1 class="guide-hero-title">
    {{ frontmatter.hero?.name }} <span class="guide-hero-product">{{ product.name }}</span>
  </h1>
  <p v-if="frontmatter.hero?.tagline" class="guide-hero-tagline">{{ frontmatter.hero.tagline }}</p>
</template>
