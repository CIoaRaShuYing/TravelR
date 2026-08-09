<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { api } from '../api'
import { clearSession } from '../session'

const router = useRouter()
const loading = ref(false)
const form = reactive({ currentPassword: '', newPassword: '', confirmPassword: '' })

async function submit() {
  if (!form.currentPassword || !form.newPassword || !form.confirmPassword) {
    ElMessage.warning('请完整填写密码信息。')
    return
  }
  if (form.newPassword !== form.confirmPassword) {
    ElMessage.warning('两次输入的新密码不一致。')
    return
  }
  loading.value = true
  try {
    await api.changePassword({ currentPassword: form.currentPassword, newPassword: form.newPassword })
    ElMessage.success('密码修改成功，请使用新密码重新登录。')
    clearSession()
    await router.replace('/claims')
  } catch (error) {
    ElMessage.error(api.message(error, '修改密码失败。'))
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <section>
    <header class="page-header">
      <div><p class="eyebrow">ACCOUNT SECURITY</p><h1>账号安全</h1><p>修改密码后，其他设备上的登录状态也会失效，需要重新登录。</p></div>
    </header>
    <div class="settings-layout" v-loading="loading">
      <section class="settings-main">
        <h2>修改登录密码</h2>
        <el-form label-position="top" @submit.prevent="submit">
          <el-form-item label="原密码"><el-input v-model="form.currentPassword" type="password" show-password autocomplete="current-password" /></el-form-item>
          <el-form-item label="新密码"><el-input v-model="form.newPassword" type="password" show-password autocomplete="new-password" placeholder="至少 8 位且包含数字" /></el-form-item>
          <el-form-item label="确认新密码"><el-input v-model="form.confirmPassword" type="password" show-password autocomplete="new-password" /></el-form-item>
          <div class="settings-actions"><el-button type="primary" native-type="submit" :loading="loading">确认修改</el-button></div>
        </el-form>
      </section>
      <aside class="policy-summary"><span>安全提示</span><strong>保护好你的登录凭据</strong><p>不要在审计记录、聊天或截图中保存密码。管理员重置密码后，请通过安全渠道通知目标用户。</p></aside>
    </div>
  </section>
</template>
