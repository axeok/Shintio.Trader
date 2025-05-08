<template>
	<v-main>
		<router-view v-slot="{ Component }">
			<component
				:is="Component"
			/>
		</router-view>
	</v-main>
</template>

<script setup>
	
</script>
