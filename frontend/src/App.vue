<template>
	<v-app>
		<router-view v-resize="onResize" />
	</v-app>
</template>

<script setup>
	const store = useAppStore();

	const onResize = () => {
		store.window.width = window.innerWidth;
		store.window.height = window.innerHeight;
	};
</script>
