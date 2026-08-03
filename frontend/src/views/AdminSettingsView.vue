<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { api, type RegistrationMode } from '../api'

const loading = ref(false)
const mode = ref<RegistrationMode>('ApprovalRequired')
const updatedAt = ref('')
const modeDescription = computed(() => ({ Open: '新用户注册后立即启用。', ApprovalRequired: '新用户提交申请，由管理员批准后启用。', Closed: '停止接收普通注册申请。' })[mode.value])

async function load() {
  loading.value = true
  try { const result = await api.getAdminSettings(); mode.value = result.registrationMode; updatedAt.value = result.updatedAt }
  catch (error) { ElMessage.error(api.message(error, '加载注册策略失败。')) }
  finally { loading.value = false }
}

async function save() {
  loading.value = true
  try { const result = await api.updateAdminSettings(mode.value); updatedAt.value = result.updatedAt; ElMessage.success('注册策略已保存。') }
  catch (error) { ElMessage.error(api.message(error, '保存注册策略失败。')) }
  finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <section>
    <header class="page-header"><div><p class="eyebrow">REGISTRATION POLICY</p><h1>注册策略</h1><p>策略只影响新的注册请求，不改变现有账户。</p></div></header>
    <div class="settings-layout" v-loading="loading">
      <section class="settings-main">
        <h2>选择注册方式</h2>
        <el-radio-group v-model="mode" class="policy-options">
          <el-radio value="Open" border><strong>开放注册</strong><span>注册后立即可用</span></el-radio>
          <el-radio value="ApprovalRequired" border><strong>管理员审批</strong><span>审批通过后可用</span></el-radio>
          <el-radio value="Closed" border><strong>关闭注册</strong><span>不接收新申请</span></el-radio>
        </el-radio-group>
        <div class="settings-actions"><el-button type="primary" :loading="loading" @click="save">保存策略</el-button></div>
      </section>
      <aside class="policy-summary"><span>当前策略</span><strong>{{ mode === 'Open' ? '开放注册' : mode === 'ApprovalRequired' ? '管理员审批' : '关闭注册' }}</strong><p>{{ modeDescription }}</p><small v-if="updatedAt">更新于 {{ new Date(updatedAt).toLocaleString('zh-CN', { hour12: false }) }}</small></aside>
    </div>
  </section>
</template>
